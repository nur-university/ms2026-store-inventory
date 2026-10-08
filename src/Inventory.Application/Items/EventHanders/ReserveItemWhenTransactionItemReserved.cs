using Inventory.Domain.Items;
using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Application.Items.EventHanders;

public class ReserveItemWhenTransactionItemReserved(IItemRepository itemRepository)
    : INotificationHandler<TransactionItemReserved>
{
    public async Task Handle(TransactionItemReserved notification, CancellationToken cancellationToken)
    {
        Item? item = await itemRepository.GetByIdAsync(notification.ItemId);
        if (item == null)
        {
            throw new DomainException(ItemErros.ItemNotFound(notification.ItemId));
        }
        item.ReserveStock(notification.Quantity);
    }
}
