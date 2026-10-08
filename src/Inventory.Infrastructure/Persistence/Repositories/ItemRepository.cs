using Inventory.Domain.Items;
using Inventory.Infrastructure.Persistence.DomainModel;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence.Repositories
{
    internal class ItemRepository : IItemRepository
    {
        private readonly DomainDbContext _dbContext;

        public ItemRepository(DomainDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(Item entity)
        {
            await _dbContext.Item.AddAsync(entity);

        }

        public async Task DeleteAsync(Guid id)
        {
            var obj = await GetByIdAsync(id);
            _dbContext.Item.Remove(obj);
        }

        public async Task<Item?> GetByIdAsync(Guid id, bool readOnly = false)
        {
            if (readOnly)
            {
                return await _dbContext.Item.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id);
            }
            else
            {
                return await _dbContext.Item.FindAsync(id);
            }
        }

        public async Task<ICollection<Item>> GetItemsFromId(ICollection<Guid> ids, bool readOnly = false)
        {
            var queryable = readOnly ? _dbContext.Item.AsNoTracking() : _dbContext.Item;
            return await queryable.Where(i => ids.Contains(i.Id)).ToListAsync();
        }

        public Task UpdateAsync(Item item)
        {
            _dbContext.Item.Update(item);

            return Task.CompletedTask;

        }
    }
}
