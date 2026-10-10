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

    /// <summary>Why the stock goes in or out. Null only for transactions created before concepts existed.</summary>
    public TransactionConcept? Concept { get; private set; }

    /// <summary>Free observation; mandatory for write-offs (what happened to the goods).</summary>
    public string? Notes { get; private set; }

    /// <summary>Who approved or rejected a transaction created by hand.</summary>
    public Guid? ReviewedBy { get; private set; }
    public string? RejectionReason { get; private set; }

    /// <summary>Created by hand (a write-off or an opening balance), not by a sale, a purchase or a return.</summary>
    public bool IsManual => SourceId == null;

    public IReadOnlyCollection<TransactionItem> Items {
        get {
            return _items;
        }
    }

    internal Transaction(Guid creatorId, TransactionType type, TransactionConcept concept, string? notes = null,
        Guid? sourceId = null, string? sourceType = null) : base(Guid.NewGuid())
    {
        notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        EnsureCoherent(type, concept, notes, sourceId, sourceType);

        CreatorId = creatorId;
        Status = TransactionStatus.Created;
        CreationDate = DateTime.UtcNow;
        TotalCost = 0;
        _items = [];
        Type = type;
        Concept = concept;
        Notes = notes;
        SourceId = sourceId;
        SourceType = sourceType;
    }

    /// <summary>R10/R11: the concept fixes the direction of the movement and the flow it may come from.</summary>
    private static void EnsureCoherent(TransactionType type, TransactionConcept concept, string? notes, Guid? sourceId, string? sourceType)
    {
        if (!Enum.IsDefined(concept))
        {
            throw new DomainException(TransactionErrors.InvalidConcept(concept.ToString()));
        }
        if (concept.TypeOf() != type)
        {
            throw new DomainException(TransactionErrors.ConceptDoesNotMatchType(concept, type));
        }

        string? expectedSource = concept.SourceTypeOf();
        bool sourceMatches = expectedSource == null
            ? sourceId == null && sourceType == null
            : sourceId != null && sourceType == expectedSource;
        if (!sourceMatches)
        {
            throw new DomainException(TransactionErrors.ConceptDoesNotMatchSource(concept, sourceType));
        }

        if (concept.RequiresNotes() && notes == null)
        {
            throw new DomainException(TransactionErrors.NotesRequired(concept));
        }
        if (notes?.Length > TransactionConceptRules.MaxNotesLength)
        {
            throw new DomainException(TransactionErrors.NotesTooLong());
        }
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

    /// <summary>
    /// Approves a transaction created by hand: a write-off leaves the warehouse, an opening balance enters it.
    /// Transactions of a sale are closed when the customer picks the goods up, not here.
    /// </summary>
    public void Approve(Guid reviewerId)
    {
        EnsurePendingManual();
        ReviewedBy = reviewerId;
        Complete();
    }

    /// <summary>Rejects a transaction created by hand: nothing moves, and the stock a write-off reserved is given back.</summary>
    public void Reject(Guid reviewerId, string? reason)
    {
        EnsurePendingManual();
        reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (reason?.Length > TransactionConceptRules.MaxNotesLength)
        {
            throw new DomainException(TransactionErrors.NotesTooLong());
        }
        ReviewedBy = reviewerId;
        RejectionReason = reason;
        Cancel();
    }

    private void EnsurePendingManual()
    {
        if (!IsManual)
        {
            throw new DomainException(TransactionErrors.NotManual(Id));
        }
        if (Status != TransactionStatus.Created)
        {
            throw new DomainException(TransactionErrors.AlreadyReviewed(Id, Status));
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
