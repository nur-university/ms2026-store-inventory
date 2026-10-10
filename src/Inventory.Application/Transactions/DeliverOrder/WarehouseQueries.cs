using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Joseco.DDD.Core.Results;
using MediatR;

namespace Inventory.Application.Transactions.DeliverOrder;

public record PendingExitLineDto(Guid ItemId, string ItemName, int Quantity);

/// <summary>A sale the warehouse has to prepare and hand over.</summary>
public record PendingExitDto(
    Guid TransactionId,
    Guid OrderId,
    DateTime CreatedOn,
    bool Paid,
    DateTime? PaidOn,
    string? InvoiceNumber,
    string? CustomerName,
    string? CustomerDocument,
    IReadOnlyList<PendingExitLineDto> Lines);

public record PagedPendingExits(IReadOnlyList<PendingExitDto> Items, int Page, int PageSize, int Total);

internal static class PendingExitMapper
{
    public static async Task<PendingExitDto> ToDtoAsync(Transaction transaction, IItemRepository items)
    {
        ICollection<Item> found = await items.GetItemsFromId(transaction.Items.Select(i => i.ItemId).ToList(), readOnly: true);

        IReadOnlyList<PendingExitLineDto> lines = transaction.Items
            .Select(i => new PendingExitLineDto(
                i.ItemId,
                found.FirstOrDefault(f => f.Id == i.ItemId)?.Name ?? string.Empty,
                i.Quantity))
            .ToList();

        return new PendingExitDto(
            transaction.Id, transaction.SourceId ?? Guid.Empty, transaction.CreationDate, transaction.ReleasedOn != null,
            transaction.ReleasedOn, transaction.InvoiceNumber, transaction.CustomerName, transaction.CustomerDocument, lines);
    }
}

// ---- what the warehouse has to prepare

public record GetPendingExitsQuery(int Page = 1, int PageSize = 20) : IRequest<Result<PagedPendingExits>>;

internal class GetPendingExitsHandler(ITransactionRepository transactions, IItemRepository items)
    : IRequestHandler<GetPendingExitsQuery, Result<PagedPendingExits>>
{
    public async Task<Result<PagedPendingExits>> Handle(GetPendingExitsQuery request, CancellationToken cancellationToken)
    {
        int page = Math.Max(1, request.Page);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);

        (IReadOnlyList<Transaction> pending, int total) = await transactions.GetPendingExitsAsync((page - 1) * pageSize, pageSize);

        List<PendingExitDto> dtos = [];
        foreach (Transaction transaction in pending)
        {
            dtos.Add(await PendingExitMapper.ToDtoAsync(transaction, items));
        }

        return new PagedPendingExits(dtos, page, pageSize, total);
    }
}

// ---- the customer shows the invoice

public record GetExitByInvoiceQuery(string InvoiceNumber) : IRequest<Result<PendingExitDto>>;

internal class GetExitByInvoiceHandler(ITransactionRepository transactions, IItemRepository items)
    : IRequestHandler<GetExitByInvoiceQuery, Result<PendingExitDto>>
{
    public async Task<Result<PendingExitDto>> Handle(GetExitByInvoiceQuery request, CancellationToken cancellationToken)
    {
        Transaction? transaction = await transactions.GetByInvoiceAsync(request.InvoiceNumber.Trim());
        if (transaction == null)
        {
            return Result.Failure<PendingExitDto>(TransactionErrors.NotFoundForInvoice(request.InvoiceNumber));
        }

        return await PendingExitMapper.ToDtoAsync(transaction, items);
    }
}

/// <summary>Hands the goods over to the customer that shows this invoice.</summary>
public record DeliverInvoiceCommand(string InvoiceNumber) : IRequest<Result<DeliveredOrder>>;

internal class DeliverInvoiceHandler(ITransactionRepository transactions, IMediator mediator)
    : IRequestHandler<DeliverInvoiceCommand, Result<DeliveredOrder>>
{
    public async Task<Result<DeliveredOrder>> Handle(DeliverInvoiceCommand request, CancellationToken cancellationToken)
    {
        Transaction? transaction = await transactions.GetByInvoiceAsync(request.InvoiceNumber.Trim());
        if (transaction?.SourceId == null)
        {
            return Result.Failure<DeliveredOrder>(TransactionErrors.NotFoundForInvoice(request.InvoiceNumber));
        }

        // The delivery itself is the same as by order: payment check, stock deduction, idempotency.
        return await mediator.Send(new DeliverOrderCommand(transaction.SourceId.Value), cancellationToken);
    }
}
