using System.Text.Json;
using Clothesline.Core.Models;

namespace Clothesline.Core.Storage;

/// <summary>
/// 绳索状态安全持久化存储器（原子写入，崩溃安全）
/// </summary>
public class StateStore : IStateStore
{
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _stateFilePath;
    private readonly string _basketFilePath;
    private readonly string _pendingCleanupFilePath;

    public StateStore(string? customBaseDirectory = null)
    {
        if (!string.IsNullOrEmpty(customBaseDirectory))
        {
            Directory.CreateDirectory(customBaseDirectory);
            _stateFilePath = Path.Combine(customBaseDirectory, "state.json");
            _basketFilePath = Path.Combine(customBaseDirectory, "basket_history.json");
            _pendingCleanupFilePath = Path.Combine(customBaseDirectory, "pending_cleanups.json");
        }
        else
        {
            AppPaths.EnsureDirectories();
            _stateFilePath = AppPaths.StateFilePath;
            _basketFilePath = AppPaths.BasketFilePath;
            _pendingCleanupFilePath = AppPaths.PendingCleanupFilePath;
        }
    }

    public async Task<List<RopeItem>> LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var path = _stateFilePath;
            if (!File.Exists(path))
            {
                return [];
            }

            var json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var items = JsonSerializer.Deserialize<List<RopeItem>>(json, JsonOptions) ?? [];
            // 自动净化：过滤磁盘上物理文件已不存在的孤儿卡片
            var validItems = items.Where(x => File.Exists(x.FilePath)).ToList();
            return validItems;
        }
        catch
        {
            return [];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(IEnumerable<RopeItem> items)
    {
        await _lock.WaitAsync();
        try
        {
            var targetPath = _stateFilePath;
            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            var json = JsonSerializer.Serialize(items, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json);

            // 原子替换，防止意外掉电损坏状态文件
            if (File.Exists(targetPath))
            {
                File.Replace(tempPath, targetPath, null);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddItemAsync(RopeItem item)
    {
        var list = await LoadAsync();
        // 按照全路径去重：如果绳上已经有了，不重复添加，更新最后访问时间
        var existing = list.FirstOrDefault(x => string.Equals(x.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.LastAccessedTime = DateTime.Now;
        }
        else
        {
            // 新卡片插入到最左侧（最新添加）
            list.Insert(0, item);
        }

        await SaveAsync(list);
    }

    public async Task RemoveItemAsync(string id)
    {
        var list = await LoadAsync();
        var target = list.FirstOrDefault(x => x.Id == id);
        if (target != null)
        {
            list.Remove(target);

            // 铁律：只从列表解挂，绝不删除源文件！
            // 如果是衍生临时副本，登记到待清理列表以便后台后续安全回收
            if (target.IsTemporaryCopy && File.Exists(target.FilePath))
            {
                await RegisterPendingCleanupAsync(target.FilePath);
            }

            await SaveAsync(list);

            // 同时自动备份一份到收纳篮子中，确保历史可追溯
            await AddToBasketAsync(target);
        }
    }

    /// <summary>
    /// 彻底从晾衣绳清除文件，不移入收纳篮；若为临时截图/转码件，安全删除物理磁盘文件
    /// </summary>
    public async Task PurgeItemAsync(string id, bool deletePhysicalTemporaryFile = true)
    {
        var list = await LoadAsync();
        var target = list.FirstOrDefault(x => x.Id == id);
        if (target != null)
        {
            list.Remove(target);
            await SaveAsync(list);

            if (deletePhysicalTemporaryFile && target.IsTemporaryCopy && File.Exists(target.FilePath))
            {
                try
                {
                    File.Delete(target.FilePath);
                }
                catch
                {
                    // 忽略删除占用的异常
                }
            }
        }
    }

    public async Task<List<RopeItem>> LoadBasketAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var path = _basketFilePath;
            if (!File.Exists(path)) return [];

            var json = await File.ReadAllTextAsync(path);
            if (string.IsNullOrWhiteSpace(json)) return [];

            var items = JsonSerializer.Deserialize<List<RopeItem>>(json, JsonOptions) ?? [];
            // 自动净化：过滤已在物理磁盘上被删除的孤儿历史记录或临时测试文件
            var validItems = items.Where(x => File.Exists(x.FilePath)).ToList();
            return validItems;
        }
        catch
        {
            return [];
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task AddToBasketAsync(RopeItem item)
    {
        var list = await LoadBasketAsync();
        var existing = list.FirstOrDefault(x => string.Equals(x.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.LastAccessedTime = DateTime.Now;
        }
        else
        {
            list.Insert(0, item);
        }

        await SaveBasketInternalAsync(list);
    }

    public async Task RemoveFromBasketAsync(string id)
    {
        var list = await LoadBasketAsync();
        var target = list.FirstOrDefault(x => x.Id == id);
        if (target != null)
        {
            list.Remove(target);
            await SaveBasketInternalAsync(list);

            // 若为临时文件，移出衣篮时彻底释放磁盘空间
            if (target.IsTemporaryCopy && File.Exists(target.FilePath))
            {
                try
                {
                    File.Delete(target.FilePath);
                }
                catch
                {
                }
            }
        }
    }

    public async Task ClearBasketAsync()
    {
        var list = await LoadBasketAsync();
        foreach (var item in list)
        {
            if (item.IsTemporaryCopy && File.Exists(item.FilePath))
            {
                try
                {
                    File.Delete(item.FilePath);
                }
                catch
                {
                }
            }
        }
        await SaveBasketInternalAsync([]);
    }

    private async Task SaveBasketInternalAsync(List<RopeItem> items)
    {
        await _lock.WaitAsync();
        try
        {
            var targetPath = _basketFilePath;
            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            var json = JsonSerializer.Serialize(items, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json);

            if (File.Exists(targetPath))
            {
                File.Replace(tempPath, targetPath, null);
            }
            else
            {
                File.Move(tempPath, targetPath);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task RegisterPendingCleanupAsync(string filePath)
    {
        try
        {
            var cleanupPath = _pendingCleanupFilePath;
            List<string> cleanups = [];
            if (File.Exists(cleanupPath))
            {
                var content = await File.ReadAllTextAsync(cleanupPath);
                cleanups = JsonSerializer.Deserialize<List<string>>(content) ?? [];
            }

            if (!cleanups.Contains(filePath))
            {
                cleanups.Add(filePath);
                await File.WriteAllTextAsync(cleanupPath, JsonSerializer.Serialize(cleanups, JsonOptions));
            }
        }
        catch
        {
            // 忽略非关键清理异常
        }
    }
}
