using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Transactions;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(Guid id);
    Task<Transaction?> GetBySourceAsync(Guid sourceId, string sourceType);
}
