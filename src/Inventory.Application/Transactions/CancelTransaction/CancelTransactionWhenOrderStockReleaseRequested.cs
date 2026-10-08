using Inventory.Domain.Transactions;
using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Application.Transactions.CancelTransaction;

/// <summary>
/// Saga compensation: the order will not be fulfilled, so the stock reserved for it is released.
/// </summary>
public class CancelTransactionWhenOrderStockReleaseRequested : IIntegrationMessageConsumer<OrderStockReleaseRequested>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CancelTransactionWhenOrderStockReleaseRequested> _logger;

    public CancelTransactionWhenOrderStockReleaseRequested(ITransactionRepository transactionRepository, IUnitOfWork unitOfWork,
        ILogger<CancelTransactionWhenOrderStockReleaseRequested> logger)
    {
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task HandleAsync(OrderStockReleaseRequested message, CancellationToken cancellationToken)
    {
        Transaction? transaction = await _transactionRepository.GetBySourceAsync(message.OrderId, "orders");
        if (transaction == null)
        {
            _logger.LogWarning("Stock release requested but no reservation exists for order {OrderId}", message.OrderId);
            return;
        }

        if (transaction.Status != TransactionStatus.Created)
        {
            return; // duplicated message, or the reservation was already confirmed/released
        }

        // Emits a TransactionItemUnreserved per item, which gives the stock back to each Item.
        transaction.Cancel();

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
