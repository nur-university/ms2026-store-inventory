using Inventory.Domain.Shared;
using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Results;
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

    /// <summary>When the sale this reservation belongs to was paid. Goods can only be delivered once it is set.</summary>
    public DateTime? ReleasedOn { get; private set; }

    /// <summary>Invoice of the paid sale, shown to the warehouse clerk when the customer picks the goods up.</summary>
    public string? InvoiceNumber { get; private set; }

    /// <summary>The customer of the sale, so the warehouse clerk can check who picks the goods up.</summary>
    public string? CustomerName { get; private set; }
    public string? CustomerDocument { get; private set; }
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

    /// <summary>Marks the reservation of a sale as paid, so the warehouse may hand the goods over. Idempotent.</summary>
    public void Release(string? invoiceNumber, string? customerName = null, string? customerDocument = null)
    {
        if (Type != TransactionType.Exit || SourceId == null)
        {
            throw new InvalidOperationException("Only an exit transaction that comes from a sale can be released");
        }
        if (Status != TransactionStatus.Created)
        {
            throw new InvalidOperationException("Cannot release a transaction that is not in Created status");
        }
        if (ReleasedOn != null)
        {
            return;
        }

        ReleasedOn = DateTime.UtcNow;
        InvoiceNumber = invoiceNumber;
        CustomerName = customerName;
        CustomerDocument = customerDocument;
    }

    public void Complete()
    {
        if (Status != TransactionStatus.Created)
        {
            throw new InvalidOperationException("Cannot complete a transaction that is not in Created status");
        }
        // Goods of a sale leave the warehouse only after the sale was paid at the cashier.
        if (SourceId.HasValue && ReleasedOn == null)
        {
            throw new DomainException(TransactionErrors.NotReleased(Id));
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

        if (SourceId.HasValue)
        {
            AddDomainEvent(new TransactionDelivered { TransactionId = Id, SourceId = SourceId.Value, SourceType = SourceType ?? string.Empty });
        }
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
