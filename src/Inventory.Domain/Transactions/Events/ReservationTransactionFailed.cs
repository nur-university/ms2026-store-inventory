using Joseco.DDD.Core.Abstractions;

namespace Inventory.Domain.Transactions.Events;

public record ReservationTransactionFailed(Guid OrderId, string Reason) : DomainEvent;
