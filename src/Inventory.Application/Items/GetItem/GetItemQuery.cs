using Inventory.Application.Items.GetItems;
using MediatR;

namespace Inventory.Application.Items.GetItem;

/// <summary>The stock of one item. Null when it does not exist.</summary>
public record GetItemQuery(Guid ItemId) : IRequest<ItemDto?>;
