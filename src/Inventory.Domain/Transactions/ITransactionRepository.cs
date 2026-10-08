using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Transactions;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(Guid id);
}
