using Inventory.Domain.Transactions.Events;
using Joseco.Communication.External.Contracts.Services;
using Joseco.Outbox.Contracts.Model;
using MediatR;
using Nur.Store2026.Integration.Inventory;

namespace Inventory.Application.Transactions.OutboxMessageHandlers;

internal class PublishOrderDelivered(IExternalPublisher publisher) : INotificationHandler<OutboxMessage<TransactionDelivered>>
{
    public async Task Handle(OutboxMessage<TransactionDelivered> outboxMessage, CancellationToken cancellationToken)
    {
        TransactionDelivered domainEvent = outboxMessage.Content;

        await publisher.PublishAsync(new OrderDelivered(domainEvent.SourceId) { Id = domainEvent.Id });
    }
}
