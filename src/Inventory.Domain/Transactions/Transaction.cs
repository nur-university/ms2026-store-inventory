using Inventory.Domain.Shared;
using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Transactions;

public class Transaction : AggregateRoot
{
    public Guid CreatorId { get; private set; }
    public DateTime CreationDate { get; private set; }
    public DateTime? CompletedDate { get; private set; }
    public DateTime? CancelDate { get; private set; }
    public CostValue TotalCost { get; private set; }

    private readonly List<TransactionItem> _items;
    public TransactionStatus Status { get; private set; }
    public TransactionType Type { get; set; }

    public Guid? SourceId { get; private set; }

    public string? SourceType { get; set; }
    public IReadOnlyCollection<TransactionItem> Items { 
        get {
            return _items;
        } 
    }

    internal Transaction(Guid creatorId, TransactionType type, Guid? sourceId = null, string? sourceType= null) : base(Guid.NewGuid())
    {
        CreatorId = creatorId;
        Status = TransactionStatus.Created;
        CreationDate = DateTime.UtcNow;
        TotalCost = 0;
        _items = [];
        Type = type;
        SourceId = sourceId;
        SourceType = sourceType;
    }

    public void AddItem(Guid itemId, int quantity, decimal unitaryCost)
    {
        if(Status != TransactionStatus.Created)
        {
            throw new InvalidOperationException("Cannot add items to a transaction that is not in Created status");
        }
        TransactionItem item = new TransactionItem(itemId, Id, quantity, unitaryCost);
        _items.Add(item);
        TotalCost += item.SubTotal;

        if(Type == TransactionType.Exit)
        {
            var domainEvent = new TransactionItemReserved(itemId, quantity, Id);
            AddDomainEvent(domainEvent);
        }
    }

    public void Complete()
    {
        if (Status != TransactionStatus.Created)
        {
            throw new InvalidOperationException("Cannot complete a transaction that is not in Created status");
        }
        if(_items.Count == 0)
        {
            throw new InvalidOperationException("Cannot complete a transaction with no items");
        }
        Status = TransactionStatus.Completed;
        CompletedDate = DateTime.UtcNow;

        List<TransactionCompleted.TransactionCompletedDetail> detail = _items
            .Select(i => new TransactionCompleted.TransactionCompletedDetail(i.ItemId, i.Quantity, i.UnitaryCost))
            .ToList();

        AddDomainEvent(new TransactionCompleted(Id, Type, detail));
    }

    public void Cancel()
    {
        if (Status != TransactionStatus.Created)
        {
            throw new InvalidOperationException("Cannot cancel a transaction that is not in Created status");
        }
        Status = TransactionStatus.Canceled;
        CancelDate = DateTime.UtcNow;

        if(Type == TransactionType.Entry)
        {
            return;
        }

        foreach (var item in _items)
        {
            AddDomainEvent(new TransactionItemUnreserved(item.ItemId, item.Quantity, Id));
        }
    }

    public void UpdateItem(Guid itemId, int quantity, decimal unitaryCost)
    {
        if (Status != TransactionStatus.Created)
        {
            throw new InvalidOperationException("Cannot update items in a transaction that is not in Created status");
        }
        TransactionItem item = _items.FirstOrDefault(i => i.ItemId == itemId);
        if (item == null)
        {
            throw new InvalidOperationException("Item not found in transaction");
        }
        TotalCost -= item.SubTotal;
        item.Update(quantity, unitaryCost);
        TotalCost += item.SubTotal;
    }

    public void RemoveItem(Guid itemId)
    {
        if (Status != TransactionStatus.Created)
        {
            throw new InvalidOperationException("Cannot remove items from a transaction that is not in Created status");
        }
        TransactionItem item = _items.FirstOrDefault(i => i.ItemId == itemId);
        if (item == null)
        {
            throw new InvalidOperationException("Item not found in transaction");
        }
        TotalCost -= item.SubTotal;
        _items.Remove(item);
    }

    //Need for EF Core
    private Transaction(){ }
}
