using Inventory.Domain.Transactions;
using Joseco.Communication.External.Contracts.Services;
using Joseco.DDD.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Application.Transactions.ReleaseTransaction;

/// <summary>
/// Saga step: the sale was paid and invoiced, so its reservation may be handed over. The stock stays reserved
/// until the customer picks the goods up at the warehouse (<see cref="DeliverOrder.DeliverOrderCommand"/>).
/// </summary>
public class ReleaseTransactionWhenOrderPaid : IIntegrationMessageConsumer<OrderPaid>
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ReleaseTransactionWhenOrderPaid> _logger;

    public ReleaseTransactionWhenOrderPaid(ITransactionRepository transactionRepository, IUnitOfWork unitOfWork,
        ILogger<ReleaseTransactionWhenOrderPaid> logger)
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
            return; // already delivered, or the reservation was released
        }

        transaction.Release(message.InvoiceNumber);

        await _unitOfWork.CommitAsync(cancellationToken);
    }
}
