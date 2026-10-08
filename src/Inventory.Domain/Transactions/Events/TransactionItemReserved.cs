using Joseco.DDD.Core.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Domain.Transactions.Events;

public record TransactionItemReserved : DomainEvent
{
    public Guid ItemId { get; init; }
    public int Quantity { get; init; }
    public Guid TransactionId { get; init; }
    public TransactionItemReserved(Guid itemId, int quantity, Guid transactionId)
    {
        ItemId = itemId;
        Quantity = quantity;
        TransactionId = transactionId;
    }

    public TransactionItemReserved() { }
}
