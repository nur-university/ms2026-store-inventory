using Inventory.Application.Transactions.DeliverOrder;
using Inventory.Application.Transactions.ReleaseTransaction;
using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Test.Application.Transactions;

/// <summary>A sale reserves stock when it is created, is released when it is paid, and is delivered when the customer picks it up.</summary>
public class PickupTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _orderId = Guid.NewGuid();

    private Transaction ReservedForTheSale()
    {
        Transaction transaction = new TransactionFactory().CreateExitTransaction(
            Guid.NewGuid(), [(Guid.NewGuid(), 2)], _orderId, "orders");
        transaction.ClearDomainEvents();
        _transactions.Setup(t => t.GetBySourceAsync(_orderId, "orders")).ReturnsAsync(transaction);
        return transaction;
    }

    private void VerifySaved(Times times) =>
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), times);

    // ---- domain

    [Fact]
    public void Release_RecordsThePaymentAndTheInvoice_Once()
    {
        Transaction transaction = ReservedForTheSale();

        transaction.Release("F-00000042");
        DateTime? firstRelease = transaction.ReleasedOn;
        transaction.Release("F-OTHER");

        Assert.NotNull(firstRelease);
        Assert.Equal(firstRelease, transaction.ReleasedOn);
        Assert.Equal("F-00000042", transaction.InvoiceNumber);
    }

    [Fact]
    public void Release_OfATransactionThatDoesNotComeFromASale_IsRejected()
    {
        Transaction entry = new TransactionFactory().CreateEntryTransaction(Guid.NewGuid(), [(Guid.NewGuid(), 1, 10m)]);

        Assert.Throws<InvalidOperationException>(() => entry.Release("F-1"));
    }

    [Fact]
    public void Complete_AnUnpaidSale_IsRejected()
    {
        Transaction transaction = ReservedForTheSale();

        DomainException ex = Assert.Throws<DomainException>(() => transaction.Complete());

        Assert.Equal("Transaction.NotReleased", ex.Error.Code);
        Assert.Equal(TransactionStatus.Created, transaction.Status);
    }

    [Fact]
    public void Complete_APaidSale_DeductsTheStockAndTellsTheSale()
    {
        Transaction transaction = ReservedForTheSale();
        transaction.Release("F-00000042");

        transaction.Complete();

        Assert.Equal(TransactionStatus.Completed, transaction.Status);
        Assert.Single(transaction.DomainEvents.OfType<TransactionCompleted>());
        TransactionDelivered delivered = Assert.Single(transaction.DomainEvents.OfType<TransactionDelivered>());
        Assert.Equal(_orderId, delivered.SourceId);
        Assert.Equal("orders", delivered.SourceType);
    }

    [Fact]
    public void Complete_ATransactionCreatedByHand_NeedsNoPayment_AndIsNotADelivery()
    {
        Transaction entry = new TransactionFactory().CreateEntryTransaction(Guid.NewGuid(), [(Guid.NewGuid(), 1, 10m)]);
        entry.ClearDomainEvents();

        entry.Complete();

        Assert.Equal(TransactionStatus.Completed, entry.Status);
        Assert.Empty(entry.DomainEvents.OfType<TransactionDelivered>());
    }

    // ---- OrderPaid consumer

    private ReleaseTransactionWhenOrderPaid Consumer() =>
        new(_transactions.Object, _unitOfWork.Object, NullLogger<ReleaseTransactionWhenOrderPaid>.Instance);

    [Fact]
    public async Task OrderPaid_ReleasesTheReservation_ButDoesNotCompleteIt()
    {
        Transaction transaction = ReservedForTheSale();

        await Consumer().HandleAsync(new OrderPaid(_orderId) { InvoiceNumber = "F-00000042" }, CancellationToken.None);

        Assert.NotNull(transaction.ReleasedOn);
        Assert.Equal("F-00000042", transaction.InvoiceNumber);
        Assert.Equal(TransactionStatus.Created, transaction.Status); // the stock is deducted at pickup, not at payment
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task OrderPaid_ForAnUnknownOrderOrADeliveredOne_IsIgnored()
    {
        _transactions.Setup(t => t.GetBySourceAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Transaction?)null);
        await Consumer().HandleAsync(new OrderPaid(Guid.NewGuid()), CancellationToken.None);

        Transaction delivered = ReservedForTheSale();
        delivered.Release("F-1");
        delivered.Complete();
        await Consumer().HandleAsync(new OrderPaid(_orderId), CancellationToken.None);

        VerifySaved(Times.Never());
    }

    // ---- pickup at the warehouse

    private DeliverOrderHandler Deliver() => new(_transactions.Object, _unitOfWork.Object);

    [Fact]
    public async Task Deliver_APaidSale_CompletesTheExit()
    {
        Transaction transaction = ReservedForTheSale();
        transaction.Release("F-00000042");

        Result<DeliveredOrder> result = await Deliver().Handle(new DeliverOrderCommand(_orderId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("F-00000042", result.Value.InvoiceNumber);
        Assert.Equal(TransactionStatus.Completed, transaction.Status);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Deliver_AnUnpaidSale_IsAConflict_AndNothingLeavesTheWarehouse()
    {
        Transaction transaction = ReservedForTheSale();

        Result<DeliveredOrder> result = await Deliver().Handle(new DeliverOrderCommand(_orderId), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Transaction.NotReleased", result.Error.Code);
        Assert.Equal(TransactionStatus.Created, transaction.Status);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Deliver_TwiceDoesNotDeductTheStockTwice()
    {
        Transaction transaction = ReservedForTheSale();
        transaction.Release("F-1");
        await Deliver().Handle(new DeliverOrderCommand(_orderId), CancellationToken.None);

        Result<DeliveredOrder> again = await Deliver().Handle(new DeliverOrderCommand(_orderId), CancellationToken.None);

        Assert.True(again.IsSuccess);
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task Deliver_ACanceledReservationOrAnUnknownSale_IsRejected()
    {
        Transaction canceled = ReservedForTheSale();
        canceled.Cancel();
        Result<DeliveredOrder> afterCancel = await Deliver().Handle(new DeliverOrderCommand(_orderId), CancellationToken.None);

        _transactions.Setup(t => t.GetBySourceAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync((Transaction?)null);
        Result<DeliveredOrder> unknown = await Deliver().Handle(new DeliverOrderCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(ErrorType.Conflict, afterCancel.Error.Type);
        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);
        VerifySaved(Times.Never());
    }
}
