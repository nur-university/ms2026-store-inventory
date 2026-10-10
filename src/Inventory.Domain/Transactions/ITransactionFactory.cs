namespace Inventory.Domain.Transactions;

public interface ITransactionFactory
{
    /// <summary>A pending entry (purchase, return or opening balance). The stock goes up when it is completed.</summary>
    Transaction CreateEntryTransaction(Guid userCreatorId, TransactionConcept concept, List<(Guid itemId, int quantity, decimal unitaryCost)> items,
        string? notes = null, Guid? sourceId = null, string? sourceType = null);

    /// <summary>A pending exit (sale or write-off). It reserves the stock; the stock goes down when it is completed.</summary>
    Transaction CreateExitTransaction(Guid userCreatorId, TransactionConcept concept, List<(Guid itemId, int quantity)> items,
        string? notes = null, Guid? sourceId = null, string? sourceType = null);
}
