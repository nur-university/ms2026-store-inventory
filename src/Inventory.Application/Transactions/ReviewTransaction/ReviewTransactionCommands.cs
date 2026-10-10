using Inventory.Domain.Transactions;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;

namespace Inventory.Application.Transactions.ReviewTransaction;

/// <summary>The outcome of approving or rejecting a transaction created by hand.</summary>
public record ReviewedTransaction(Guid TransactionId, string Status, string? Concept, Guid? ReviewedBy, DateTime? CompletedDate, DateTime? CancelDate,
    string? RejectionReason);

/// <summary>
/// Approves a pending write-off (the reserved stock leaves the warehouse) or opening balance (the stock goes up).
/// Approving it again is harmless; approving a rejected one is a conflict.
/// </summary>
public record ApproveTransactionCommand(Guid TransactionId, Guid ReviewerId) : IRequest<Result<ReviewedTransaction>>;

/// <summary>Rejects a pending write-off (its reserved stock is given back) or opening balance. Rejecting it again is harmless.</summary>
public record RejectTransactionCommand(Guid TransactionId, Guid ReviewerId, string? Reason) : IRequest<Result<ReviewedTransaction>>;

internal class ApproveTransactionHandler(ITransactionRepository transactions, IUnitOfWork unitOfWork)
    : IRequestHandler<ApproveTransactionCommand, Result<ReviewedTransaction>>
{
    public Task<Result<ReviewedTransaction>> Handle(ApproveTransactionCommand request, CancellationToken cancellationToken) =>
        TransactionReview.ApplyAsync(transactions, unitOfWork, request.TransactionId, TransactionStatus.Completed,
            transaction => transaction.Approve(request.ReviewerId), cancellationToken);
}

internal class RejectTransactionHandler(ITransactionRepository transactions, IUnitOfWork unitOfWork)
    : IRequestHandler<RejectTransactionCommand, Result<ReviewedTransaction>>
{
    public Task<Result<ReviewedTransaction>> Handle(RejectTransactionCommand request, CancellationToken cancellationToken) =>
        TransactionReview.ApplyAsync(transactions, unitOfWork, request.TransactionId, TransactionStatus.Canceled,
            transaction => transaction.Reject(request.ReviewerId, request.Reason), cancellationToken);
}

internal static class TransactionReview
{
    public static async Task<Result<ReviewedTransaction>> ApplyAsync(ITransactionRepository transactions, IUnitOfWork unitOfWork,
        Guid transactionId, TransactionStatus outcome, Action<Transaction> review, CancellationToken cancellationToken)
    {
        Transaction? transaction = await transactions.GetByIdAsync(transactionId);
        if (transaction == null)
        {
            return Result.Failure<ReviewedTransaction>(TransactionErrors.NotFound(transactionId));
        }

        // A repeated request (a double click, a retry) must not move the stock twice.
        if (transaction.IsManual && transaction.Status == outcome)
        {
            return ToDto(transaction);
        }

        try
        {
            review(transaction);
            // Approving moves the stock (TransactionCompleted); rejecting a write-off gives the reservation back.
            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch (DomainException ex)
        {
            return Result.Failure<ReviewedTransaction>(ex.Error);
        }

        return ToDto(transaction);
    }

    private static ReviewedTransaction ToDto(Transaction t) =>
        new(t.Id, t.Status.ToString(), t.Concept?.ToString(), t.ReviewedBy, t.CompletedDate, t.CancelDate, t.RejectionReason);
}
