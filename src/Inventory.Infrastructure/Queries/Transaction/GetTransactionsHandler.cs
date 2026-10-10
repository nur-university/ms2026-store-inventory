using Inventory.Application.Transactions.GetTransactions;
using Inventory.Infrastructure.Persistence.StoredModel;
using Inventory.Infrastructure.Persistence.StoredModel.Entities;
using Joseco.DDD.Core.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Queries.Transaction;

internal class GetTransactionsHandler(StoredDbContext dbContext) : IRequestHandler<GetTransactionsQuery, Result<PagedTransactions>>
{
    public async Task<Result<PagedTransactions>> Handle(GetTransactionsQuery request, CancellationToken cancellationToken)
    {
        Result<TransactionFilter> parsed = TransactionFilter.Create(request);
        if (parsed.IsFailure)
        {
            return Result.Failure<PagedTransactions>(parsed.Error);
        }
        TransactionFilter filter = parsed.Value;

        IQueryable<TransactionStoredModel> query = dbContext.Transaction.AsNoTracking();
        if (filter.Type is { } type)
        {
            string value = type.ToString();
            query = query.Where(t => t.TransactionType == value);
        }
        if (filter.Concept is { } concept)
        {
            string value = concept.ToString();
            query = query.Where(t => t.Concept == value);
        }
        if (filter.Status is { } status)
        {
            string value = status.ToString();
            query = query.Where(t => t.Status == value);
        }
        if (filter.SourceType != null)
        {
            query = query.Where(t => t.SourceType == filter.SourceType);
        }
        if (filter.From is { } from)
        {
            query = query.Where(t => t.CreationDate >= from);
        }
        if (filter.ToExclusive is { } to)
        {
            query = query.Where(t => t.CreationDate < to);
        }

        int total = await query.CountAsync(cancellationToken);

        List<TransactionSummaryDto> items = await query
            .OrderByDescending(t => t.CreationDate).ThenBy(t => t.Id)
            .Skip(filter.Skip).Take(filter.PageSize)
            .Select(t => new TransactionSummaryDto(
                t.Id, t.TransactionType, t.Concept, t.Status, t.CreationDate, t.CompletedDate, t.CancelDate, t.UserCreatorId,
                t.SourceId, t.SourceType, t.InvoiceNumber, t.Notes, t.TotalCost,
                t.Items.Count, t.Items.Sum(i => i.Quantity)))
            .ToListAsync(cancellationToken);

        return new PagedTransactions(items, filter.Page, filter.PageSize, total);
    }
}
