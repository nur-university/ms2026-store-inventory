using Inventory.Domain.Transactions.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Domain.Transactions;

public class TransactionFactory : ITransactionFactory
{
    public Transaction CreateEntryTransaction(Guid userCreatorId, List<(Guid itemId, int quantity, decimal unitaryCost)> items, Guid? sourceId = null, string? sourceType = null)
    {
        if(userCreatorId == Guid.Empty)
        {
            throw new ArgumentException("User creator id is required");
        }
        var transaction = new Transaction(userCreatorId, TransactionType.Entry, sourceId, sourceType);

        foreach (var item in items)
        {
            transaction.AddItem(item.itemId, item.quantity, item.unitaryCost);
        }


        return transaction;
    }

    public Transaction CreateExitTransaction(Guid userCreatorId, List<(Guid itemId, int quantity)> items, Guid? sourceId = null, string? sourceType = null)
    {
        if (userCreatorId == Guid.Empty)
        {
            throw new ArgumentException("User creator id is required");
        }
        var transaction = new Transaction(userCreatorId, TransactionType.Exit, sourceId, sourceType);

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
