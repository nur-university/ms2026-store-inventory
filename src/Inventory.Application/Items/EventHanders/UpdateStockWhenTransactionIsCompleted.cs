using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Events;
using MediatR;
using Joseco.DDD.Core.Abstractions;

namespace Inventory.Application.Items.EventHanders;

public class UpdateStockWhenTransactionIsCompleted : INotificationHandler<TransactionCompleted>
{
    private readonly IItemRepository _itemRepository;

    public UpdateStockWhenTransactionIsCompleted(IItemRepository itemRepository) {
        _itemRepository = itemRepository;
    }

    public async Task Handle(TransactionCompleted domainEvent, CancellationToken cancellationToken) {
        bool isEntry = domainEvent.TransactionType == TransactionType.Entry;
        foreach (var item in domainEvent.Details)
        {
            var itemEntity = await _itemRepository.GetByIdAsync(item.ItemId);
            if (itemEntity == null)
            {
                continue;
            }

            if (isEntry)
            {
                itemEntity.AddStock(item.Quantity, item.unitaryCost);
            }
            else
            {
                itemEntity.ApplyReservation(item.Quantity);
            }
        }

    }
}
