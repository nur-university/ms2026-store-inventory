
using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Items;

public interface IItemRepository : IRepository<Item>
{
    Task UpdateAsync(Item item);
    Task DeleteAsync(Guid id);

    Task<ICollection<Item>> GetItemsFromId(ICollection<Guid> ids, bool readOnly = false);
}
