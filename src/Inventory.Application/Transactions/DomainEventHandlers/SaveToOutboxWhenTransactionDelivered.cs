using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Abstractions;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using MediatR;

namespace Inventory.Application.Transactions.DomainEventHandlers;

internal class SaveToOutboxWhenTransactionDelivered(IOutboxService<DomainEvent> outboxService)
    : INotificationHandler<TransactionDelivered>
{
    public async Task Handle(TransactionDelivered domainEvent, CancellationToken cancellationToken)
    {
        if (domainEvent.SourceType != "orders")
        {
            return;
        }

        await outboxService.AddAsync(new OutboxMessage<DomainEvent>(domainEvent));
    }
}
