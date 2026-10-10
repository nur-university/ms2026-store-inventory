using Inventory.Application.Items.GetItem;
using Inventory.Application.Items.GetItems;
using Inventory.Infrastructure.Persistence.StoredModel;
using Inventory.Infrastructure.Persistence.StoredModel.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Queries.Item;

internal class GetItemsHandler : IRequestHandler<GetItemsQuery, IEnumerable<ItemDto>>,
                                 IRequestHandler<GetItemQuery, ItemDto?>
{
    private readonly StoredDbContext _dbContext;

    public GetItemsHandler(StoredDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IEnumerable<ItemDto>> Handle(GetItemsQuery request, CancellationToken cancellationToken)
    {
        return await ToDto(_dbContext.Item.AsNoTracking().OrderBy(i => i.ItemName))
            .ToListAsync(cancellationToken);
    }

    public Task<ItemDto?> Handle(GetItemQuery request, CancellationToken cancellationToken)
    {
        return ToDto(_dbContext.Item.AsNoTracking().Where(i => i.Id == request.ItemId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static IQueryable<ItemDto> ToDto(IQueryable<ItemStoredModel> items) =>
        items.Select(i => new ItemDto()
        {
            Id = i.Id,
            ItemName = i.ItemName,
            Stock = i.Stock,
            Reserved = i.Reserverd,
            Available = i.Available,
            UnitaryCost = i.UnitaryCost
        });
}
