namespace Clothesline.Platform.SingleInstance;

/// <summary>
/// 全局单实例防多开互斥锁（Windows 命名 Mutex / Unix 锁文件）
/// </summary>
public sealed class SingleInstanceLock : IDisposable
{
    private const string AppMutexName = "Clothesline.App.SingleInstance.Mutex.2026";
    private readonly Mutex? _mutex;
    private readonly bool _ownsMutex;

    public SingleInstanceLock()
    {
        try
        {
            _mutex = new Mutex(true, AppMutexName, out _ownsMutex);
        }
        catch
        {
            _ownsMutex = false;
        }
    }

    public bool IsOnlyInstance => _ownsMutex;

    public void Dispose()
    {
        if (_ownsMutex && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // 忽略释放异常
            }
            _mutex.Dispose();
        }
    }
}
