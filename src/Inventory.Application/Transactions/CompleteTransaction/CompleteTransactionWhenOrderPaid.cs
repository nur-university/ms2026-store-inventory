using Inventory.Domain.Transactions;
using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Application.Transactions.CompleteTransaction;

/// <summary>
/// Saga step: the order was paid, so the stock reserved for it is confirmed (deducted).
/// </summary>
public class CompleteTransactionWhenOrderPaid : IIntegrationMessageConsumer<OrderPaid>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CompleteTransactionWhenOrderPaid> _logger;

    public CompleteTransactionWhenOrderPaid(ITransactionRepository transactionRepository, IUnitOfWork unitOfWork,
        ILogger<CompleteTransactionWhenOrderPaid> logger)
    {
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(OrderPaid message, CancellationToken cancellationToken)
    {
        Transaction? transaction = await _transactionRepository.GetBySourceAsync(message.OrderId, "orders");
        if (transaction == null)
        {
            _logger.LogWarning("OrderPaid received but no reservation exists for order {OrderId}", message.OrderId);
            return;
        }

        if (transaction.Status != TransactionStatus.Created)
        {
            return; // duplicated message, or the reservation was already released
        }

        transaction.Complete();

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
