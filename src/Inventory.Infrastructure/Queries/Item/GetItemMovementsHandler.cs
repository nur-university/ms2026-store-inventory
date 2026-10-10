using Inventory.Application.Items.GetItemMovements;
using Inventory.Application.Transactions.GetTransactions;
using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Infrastructure.Persistence.StoredModel;
using Joseco.DDD.Core.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Queries.Item;

internal class GetItemMovementsHandler(StoredDbContext dbContext) : IRequestHandler<GetItemMovementsQuery, Result<ItemKardexDto>>
{
    private static readonly string Completed = TransactionStatus.Completed.ToString();

    public async Task<Result<ItemKardexDto>> Handle(GetItemMovementsQuery request, CancellationToken cancellationToken)
    {
        var item = await dbContext.Item.AsNoTracking()
            .Where(i => i.Id == request.ItemId)
            .Select(i => new { i.ItemName, i.Stock, i.Reserverd, i.Available, i.UnitaryCost })
            .FirstOrDefaultAsync(cancellationToken);
        if (item == null)
        {
            return Result.Failure<ItemKardexDto>(ItemErros.ItemNotFound(request.ItemId));
        }

        (DateTime? from, DateTime? to) = QueryFilters.DateRange(request.From, request.To);
        if (from != null && to != null && from >= to)
        {
            return Result.Failure<ItemKardexDto>(QueryFilters.InvalidRange());
        }
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 200);
        int skip = (page - 1) * pageSize;

        // Only completed transactions moved the stock; a movement happens when the transaction is completed.
        var movements = dbContext.TransactionItem.AsNoTracking()
            .Where(ti => ti.ItemId == request.ItemId && ti.Transaction.Status == Completed)
            .Select(ti => new
            {
                Date = ti.Transaction.CompletedDate ?? ti.Transaction.CreationDate,
                ti.TransactionId,
                LineId = ti.Id,
                Type = ti.Transaction.TransactionType,
                ti.Transaction.Concept,
                ti.Transaction.SourceId,
                ti.Transaction.SourceType,
                ti.Transaction.InvoiceNumber,
                ti.Transaction.Notes,
                ti.Quantity,
                ti.UnitaryCost,
                Signed = ti.Transaction.TransactionType == Kardex.Entry ? ti.Quantity : -ti.Quantity
            });

        // Balance before the range: every earlier movement.
        int balanceBefore = from is { } start
            ? await movements.Where(m => m.Date < start).SumAsync(m => m.Signed, cancellationToken)
            : 0;

        var inRange = movements;
        if (from is { } lower)
        {
            inRange = inRange.Where(m => m.Date >= lower);
        }
        if (to is { } upper)
        {
            inRange = inRange.Where(m => m.Date < upper);
        }
        var ordered = inRange.OrderBy(m => m.Date).ThenBy(m => m.TransactionId).ThenBy(m => m.LineId);

        int total = await inRange.CountAsync(cancellationToken);

        // ... plus the movements of the range shown on earlier pages.
        if (skip > 0)
        {
            balanceBefore += await ordered.Take(skip).SumAsync(m => m.Signed, cancellationToken);
        }

        List<ItemMovementRow> rows = await ordered.Skip(skip).Take(pageSize)
            .Select(m => new ItemMovementRow(m.Date, m.TransactionId, m.Type, m.Concept, m.SourceId, m.SourceType, m.InvoiceNumber,
                m.Notes, m.Quantity, m.UnitaryCost))
            .ToListAsync(cancellationToken);

        IReadOnlyList<ItemMovementDto> pageMovements = Kardex.Accumulate(balanceBefore, rows);
        int balanceAfter = pageMovements.Count > 0 ? pageMovements[^1].Balance : balanceBefore;

        return new ItemKardexDto(request.ItemId, item.ItemName, item.Stock, item.Reserverd, item.Available, item.UnitaryCost,
            balanceBefore, balanceAfter, pageMovements, page, pageSize, total);
    }
}
