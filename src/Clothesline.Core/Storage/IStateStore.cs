using Clothesline.Core.Models;

namespace Clothesline.Core.Storage;

public interface IStateStore
{
    Task<List<RopeItem>> LoadAsync();
    Task SaveAsync(IEnumerable<RopeItem> items);
    Task AddItemAsync(RopeItem item);
    Task RemoveItemAsync(string id);
    Task PurgeItemAsync(string id, bool deletePhysicalTemporaryFile = true);

    Task<List<RopeItem>> LoadBasketAsync();
    Task AddToBasketAsync(RopeItem item);
    Task RemoveFromBasketAsync(string id);
    Task ClearBasketAsync();
}
