using Inventory.Domain.Transactions.Events;

namespace Inventory.Domain.Transactions;

public class TransactionFactory : ITransactionFactory
{
    public Transaction CreateEntryTransaction(Guid userCreatorId, TransactionConcept concept, List<(Guid itemId, int quantity, decimal unitaryCost)> items,
        string? notes = null, Guid? sourceId = null, string? sourceType = null)
    {
        if(userCreatorId == Guid.Empty)
        {
            throw new ArgumentException("User creator id is required");
        }
        var transaction = new Transaction(userCreatorId, TransactionType.Entry, concept, notes, sourceId, sourceType);

        foreach (var item in items)
        {
            transaction.AddItem(item.itemId, item.quantity, item.unitaryCost);
        }


        return transaction;
    }

    public Transaction CreateExitTransaction(Guid userCreatorId, TransactionConcept concept, List<(Guid itemId, int quantity)> items,
        string? notes = null, Guid? sourceId = null, string? sourceType = null)
    {
        if (userCreatorId == Guid.Empty)
        {
            throw new ArgumentException("User creator id is required");
        }
        var transaction = new Transaction(userCreatorId, TransactionType.Exit, concept, notes, sourceId, sourceType);

        foreach (var item in items)
        {
            transaction.AddItem(item.itemId, item.quantity, 0);
        }

        if(sourceId != null && sourceType != null)
        {
            transaction.AddDomainEvent(new TransactionReserved()
            {
                SourceId = sourceId.Value,
                SourceType = sourceType,
            });
        }


        return transaction;
    }
}
