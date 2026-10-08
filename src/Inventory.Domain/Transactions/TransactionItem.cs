using Inventory.Domain.Shared;
using Joseco.DDD.Core.Abstractions;
using System.Transactions;

namespace Inventory.Domain.Transactions;

public class TransactionItem : Entity
{
    public Guid  ItemId { get; private set; }
    public CostValue UnitaryCost { get; private set; }
    public CostValue SubTotal { get; private set; }

    public QuantityValue Quantity { get; private set; }

    public Guid TransactionId { get; private set; }

    public TransactionItem(Guid itemId, Guid transactionId, int quantity, decimal unitaryCost) : base(Guid.NewGuid())
    {
        ItemId = itemId;
        TransactionId = transactionId;
        Quantity = quantity;
        UnitaryCost = unitaryCost;
        SubTotal = Quantity * UnitaryCost;
    }

    internal void Update(int quantity, decimal unitaryCost)
    {
        Quantity = quantity;
        UnitaryCost = unitaryCost;
        SubTotal = Quantity * UnitaryCost;
    }

    //Need for EF Core
    private TransactionItem() { }
}
