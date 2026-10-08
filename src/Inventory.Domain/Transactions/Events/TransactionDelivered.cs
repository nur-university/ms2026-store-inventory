using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Transactions.Events;

/// <summary>An exit transaction that came from another service (a sale) was completed: the customer picked the goods up.</summary>
public record TransactionDelivered : DomainEvent
{
    public Guid TransactionId { get; init; }
    public Guid SourceId { get; init; }
    public string SourceType { get; init; } = string.Empty;
}
