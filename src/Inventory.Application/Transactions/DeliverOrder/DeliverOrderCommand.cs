using Inventory.Domain.Transactions;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;

namespace Inventory.Application.Transactions.DeliverOrder;

/// <summary>The customer picks up the goods of a paid sale at the warehouse.</summary>
public record DeliverOrderCommand(Guid OrderId) : IRequest<Result<DeliveredOrder>>;

public record DeliveredOrder(Guid OrderId, Guid TransactionId, string? InvoiceNumber, DateTime? DeliveredOn);

internal class DeliverOrderHandler(ITransactionRepository transactionRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<DeliverOrderCommand, Result<DeliveredOrder>>
{
    public async Task<Result<DeliveredOrder>> Handle(DeliverOrderCommand request, CancellationToken cancellationToken)
    {
        Transaction? transaction = await transactionRepository.GetBySourceAsync(request.OrderId, "orders");
        if (transaction == null)
        {
            return Result.Failure<DeliveredOrder>(TransactionErrors.NotFoundForSource(request.OrderId, "orders"));
        }

        // Handing the goods over twice must not deduct the stock twice.
        if (transaction.Status == TransactionStatus.Completed)
        {
            return new DeliveredOrder(request.OrderId, transaction.Id, transaction.InvoiceNumber, transaction.CompletedDate);
        }

        if (transaction.Status != TransactionStatus.Created)
        {
            return Result.Failure<DeliveredOrder>(TransactionErrors.NotPending(transaction.Id, transaction.Status));
        }

        try
        {
            // Completing the exit deducts the stock; it needs the sale to be paid (released).
            transaction.Complete();
        }
        catch (DomainException ex)
        {
            return Result.Failure<DeliveredOrder>(ex.Error);
        }

        await unitOfWork.CommitAsync(cancellationToken);

        return new DeliveredOrder(request.OrderId, transaction.Id, transaction.InvoiceNumber, transaction.CompletedDate);
    }
}
