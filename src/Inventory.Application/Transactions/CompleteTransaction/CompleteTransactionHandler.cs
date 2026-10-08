using Inventory.Domain.Transactions;
using MediatR;
using Joseco.DDD.Core.Abstractions;

namespace Inventory.Application.Transactions.CompleteTransaction;

internal class CompleteTransactionHandler : IRequestHandler<CompleteTransactionCommand, bool>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteTransactionHandler(ITransactionRepository transactionRepository, IUnitOfWork unitOfWork) {
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(CompleteTransactionCommand request, CancellationToken cancellationToken) {
        var transaction = await _transactionRepository.GetByIdAsync(request.TransactionId);

        if (transaction == null)
        {
            throw new InvalidOperationException("Transaction not found");
        }

        transaction.Complete();

        await _unitOfWork.CommitAsync(cancellationToken);

        return true;

    }
}
