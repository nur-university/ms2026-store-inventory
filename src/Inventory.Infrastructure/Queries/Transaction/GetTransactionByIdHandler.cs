using Inventory.Application.Transactions.GetTransaccionById;
using Inventory.Infrastructure.Persistence.StoredModel;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Queries.Transaction;

internal class GetTransactionByIdHandler : IRequestHandler<GetTransaccionByIdQuery, TransactionDto?>
{
    private readonly StoredDbContext _dbContext;

    public GetTransactionByIdHandler(StoredDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TransactionDto?> Handle(GetTransaccionByIdQuery request, CancellationToken cancellationToken)
    {
        if (request.TransactionId == Guid.Empty)
        {
            return null;
        }

        return await _dbContext.Transaction.AsNoTracking()
            .Where(t => t.Id == request.TransactionId)
            .Select(t => new TransactionDto()
            {
                Id = t.Id,
                Type = t.TransactionType,
                Concept = t.Concept,
                Status = t.Status,
                CreationDate = t.CreationDate,
                CompletedDate = t.CompletedDate,
                CancelDate = t.CancelDate,
                CreatorId = t.UserCreatorId,
                CreatorName = t.UserCreator.FullName,
                SourceId = t.SourceId,
                SourceType = t.SourceType,
                Notes = t.Notes,
                ReviewedBy = t.ReviewedBy,
                RejectionReason = t.RejectionReason,
                ReleasedOn = t.ReleasedOn,
                InvoiceNumber = t.InvoiceNumber,
                CustomerName = t.CustomerName,
                CustomerDocument = t.CustomerDocument,
                TotalCost = t.TotalCost,
                Lines = t.Items.Select(i => new TransactionLineDto
                {
                    ItemId = i.ItemId,
                    ItemName = i.Item.ItemName,
                    Quantity = i.Quantity,
                    UnitaryCost = i.UnitaryCost,
                    SubTotal = i.SubTotal
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }
}
