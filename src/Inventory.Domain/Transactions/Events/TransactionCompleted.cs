using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Transactions.Events;

public record TransactionCompleted : DomainEvent
{
    public Guid TransactionId { get; init; }
    public TransactionType TransactionType { get; init; }

    public ICollection<TransactionCompletedDetail> Details { get; init; }

    public TransactionCompleted(Guid transactionId, 
        TransactionType type, 
        ICollection<TransactionCompletedDetail> details)
    {
        TransactionId = transactionId;
        TransactionType = type;
        Details = details;
    }

    private TransactionCompleted() { }

    public record TransactionCompletedDetail(Guid ItemId, int Quantity, decimal unitaryCost);
}
