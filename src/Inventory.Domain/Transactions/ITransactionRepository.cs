using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Transactions;

public interface ITransactionRepository : IRepository<Transaction>
{
    Task UpdateAsync(Transaction transaction);
    Task DeleteAsync(Guid id);
    Task<Transaction?> GetBySourceAsync(Guid sourceId, string sourceType);

    /// <summary>The reservation of the sale that was invoiced with that number.</summary>
    Task<Transaction?> GetByInvoiceAsync(string invoiceNumber);

    /// <summary>Sales whose goods the warehouse still has to prepare and hand over, oldest first.</summary>
    Task<(IReadOnlyList<Transaction> Transactions, int Total)> GetPendingExitsAsync(int skip, int take);
}
