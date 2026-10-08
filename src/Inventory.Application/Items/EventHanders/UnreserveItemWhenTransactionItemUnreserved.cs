using Inventory.Domain.Items;
using Inventory.Domain.Transactions.Events;
using MediatR;

namespace Inventory.Application.Items.EventHanders;

public class UnreserveItemWhenTransactionItemUnreserved(IItemRepository itemRepository)
    : INotificationHandler<TransactionItemUnreserved>
{
    public async Task Handle(TransactionItemUnreserved notification, CancellationToken cancellationToken)
    {
        Item? item = await itemRepository.GetByIdAsync(notification.ItemId);
        if (item == null)
        {
            return;
        }
        item.UnreserveStock(notification.Quantity);
    }
}
