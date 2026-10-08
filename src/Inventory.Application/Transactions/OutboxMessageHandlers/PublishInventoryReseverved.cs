using Inventory.Domain.Transactions.Events;
using Joseco.Communication.External.Contracts.Services;
using Joseco.Outbox.Contracts.Model;
using MediatR;
using Nur.Store2026.Integration.Inventory;

internal class PublishInventoryReseverved : INotificationHandler<OutboxMessage<TransactionReserved>>
{
    private readonly IExternalPublisher _publisher;

    public PublishInventoryReseverved(IExternalPublisher publisher) {
        _publisher = publisher;
    }

    public async Task Handle(OutboxMessage<TransactionReserved> outboxMessage, CancellationToken cancellationToken) {

        var conent = outboxMessage.Content;

        InventoryReserved message = new InventoryReserved()
        {
            Id = conent.Id,
            SourceId = conent.SourceId,
            SourceType = conent.SourceType
        };

        await _publisher.PublishAsync(message);
    }
}
