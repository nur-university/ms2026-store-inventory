using Inventory.Domain.Transactions.Events;
using Joseco.Communication.External.Contracts.Services;
using Joseco.Outbox.Contracts.Model;
using MediatR;
using Nur.Store2026.Integration.Inventory;

namespace Inventory.Application.Transactions.OutboxMessageHandlers;

internal class PublishInventoryReservationFailed :INotificationHandler<OutboxMessage<ReservationTransactionFailed>>
{
    private readonly IExternalPublisher _publisher;

    public PublishInventoryReservationFailed(IExternalPublisher publisher) {
        _publisher = publisher;
    }

    public async Task Handle(OutboxMessage<ReservationTransactionFailed> outboxMessage, CancellationToken cancellationToken) 
    {
        var domainEvent = outboxMessage.Content;

        InventoryReservationFailed inventoryReservationFailed = new()
        {
            OrderId = domainEvent.OrderId,
            Reason = domainEvent.Reason
        };

        // The exchange (inventory-reservation-failed) is declared in rabbitmq/definitions.json; redeclaring it
        // here with different arguments is rejected by the broker (PRECONDITION_FAILED).
        await _publisher.PublishAsync(inventoryReservationFailed);
    }
}
