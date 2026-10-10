using Inventory.Application.Items.EventHanders;
using Inventory.Application.Transactions.ReviewTransaction;
using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Moq;

namespace Inventory.Test.Application.Transactions;

/// <summary>5.2: a write-off is created pending (reserves the stock) and is approved (deducts it) or rejected (gives it back).</summary>
public class WriteOffReviewTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IItemRepository> _items = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _manager = Guid.NewGuid();
    private readonly Item _tv = new(Guid.NewGuid(), "Smart TV");

    public WriteOffReviewTests()
    {
        _tv.AddStock(10, 100m);
        _items.Setup(i => i.GetByIdAsync(_tv.Id, false)).ReturnsAsync(_tv);
    }

    private Transaction Registered(Transaction transaction)
    {
        _transactions.Setup(t => t.GetByIdAsync(transaction.Id, false)).ReturnsAsync(transaction);
        return transaction;
    }

    private Transaction PendingWriteOff(int quantity = 3) =>
        Registered(new TransactionFactory().CreateExitTransaction(Guid.NewGuid(), TransactionConcept.Damaged, [(_tv.Id, quantity)], "fell off the shelf"));

    /// <summary>Plays the domain events the UnitOfWork would publish, against the real stock handlers.</summary>
    private async Task PublishEventsAsync(Transaction transaction)
    {
        foreach (DomainEvent domainEvent in transaction.DomainEvents.ToList())
        {
            Task handled = domainEvent switch
            {
                TransactionItemReserved e => new ReserveItemWhenTransactionItemReserved(_items.Object).Handle(e, CancellationToken.None),
                TransactionItemUnreserved e => new UnreserveItemWhenTransactionItemUnreserved(_items.Object).Handle(e, CancellationToken.None),
                TransactionCompleted e => new UpdateStockWhenTransactionIsCompleted(_items.Object).Handle(e, CancellationToken.None),
                _ => Task.CompletedTask
            };
            await handled;
        }
        transaction.ClearDomainEvents();
    }

    private string Stock() => $"{_tv.Stock.Value}/{_tv.Reserved.Value}/{_tv.Available.Value}";

    private Task<Result<ReviewedTransaction>> Approve(Guid id) =>
        new ApproveTransactionHandler(_transactions.Object, _unitOfWork.Object).Handle(new ApproveTransactionCommand(id, _manager), CancellationToken.None);

    private Task<Result<ReviewedTransaction>> Reject(Guid id, string? reason = "it works after all") =>
        new RejectTransactionHandler(_transactions.Object, _unitOfWork.Object).Handle(new RejectTransactionCommand(id, _manager, reason), CancellationToken.None);

    private void VerifySaved(Times times) => _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task AWriteOff_ReservesTheStock_AndItsApprovalDeductsIt()
    {
        Transaction writeOff = PendingWriteOff();
        await PublishEventsAsync(writeOff);
        Assert.Equal("10/3/7", Stock());

        Result<ReviewedTransaction> result = await Approve(writeOff.Id);
        await PublishEventsAsync(writeOff);

        Assert.True(result.IsSuccess);
        Assert.Equal("Completed", result.Value.Status);
        Assert.Equal("Damaged", result.Value.Concept);
        Assert.Equal(_manager, result.Value.ReviewedBy);
        Assert.Equal("7/0/7", Stock());
        VerifySaved(Times.Once());
    }

    [Fact]
    public async Task ARejectedWriteOff_GivesTheStockBack()
    {
        Transaction writeOff = PendingWriteOff();
        await PublishEventsAsync(writeOff);

        Result<ReviewedTransaction> result = await Reject(writeOff.Id, "  it works after all ");
        await PublishEventsAsync(writeOff);

        Assert.Equal("Canceled", result.Value.Status);
        Assert.Equal("it works after all", result.Value.RejectionReason);
        Assert.Equal(_manager, writeOff.ReviewedBy);
        Assert.Equal("10/0/10", Stock());
    }

    [Fact]
    public async Task AnApprovedOpeningBalance_AddsStockAtItsCost()
    {
        Transaction opening = Registered(new TransactionFactory().CreateEntryTransaction(Guid.NewGuid(), TransactionConcept.OpeningBalance,
            [(_tv.Id, 10, 200m)]));
        await PublishEventsAsync(opening);
        Assert.Equal("10/0/10", Stock()); // nothing moves while it is pending

        await Approve(opening.Id);
        await PublishEventsAsync(opening);

        Assert.Equal("20/0/20", Stock());
        Assert.Equal(150m, _tv.UnitaryCost.Value);
    }

    [Fact]
    public async Task ApprovingOrRejectingTwice_IsHarmless()
    {
        Transaction approved = PendingWriteOff();
        Transaction rejected = PendingWriteOff();

        await Approve(approved.Id);
        await Reject(rejected.Id);
        Result<ReviewedTransaction> approvedAgain = await Approve(approved.Id);
        Result<ReviewedTransaction> rejectedAgain = await Reject(rejected.Id);

        Assert.True(approvedAgain.IsSuccess);
        Assert.True(rejectedAgain.IsSuccess);
        VerifySaved(Times.Exactly(2));
    }

    [Fact]
    public async Task TheOppositeDecision_IsAConflict()
    {
        Transaction approved = PendingWriteOff();
        Transaction rejected = PendingWriteOff();
        await Approve(approved.Id);
        await Reject(rejected.Id);

        Result<ReviewedTransaction> rejectApproved = await Reject(approved.Id);
        Result<ReviewedTransaction> approveRejected = await Approve(rejected.Id);

        Assert.Equal(ErrorType.Conflict, rejectApproved.Error.Type);
        Assert.Equal("Transaction.AlreadyReviewed", approveRejected.Error.Code);
        Assert.Equal(TransactionStatus.Completed, approved.Status);
        Assert.Equal(TransactionStatus.Canceled, rejected.Status);
        VerifySaved(Times.Exactly(2));
    }

    [Fact]
    public async Task TheReservationOfASale_IsNotApprovedOrRejectedByHand()
    {
        Transaction sale = Registered(new TransactionFactory().CreateExitTransaction(Guid.NewGuid(), TransactionConcept.Sale, [(_tv.Id, 1)],
            sourceId: Guid.NewGuid(), sourceType: TransactionSources.Orders));
        sale.Release("F-00000001");

        Result<ReviewedTransaction> approve = await Approve(sale.Id);
        Result<ReviewedTransaction> reject = await Reject(sale.Id);

        Assert.Equal("Transaction.NotManual", approve.Error.Code);
        Assert.Equal(ErrorType.Conflict, reject.Error.Type);
        Assert.Equal(TransactionStatus.Created, sale.Status);
        VerifySaved(Times.Never());
    }

    [Fact]
    public async Task AnUnknownTransaction_IsNotFound()
    {
        Result<ReviewedTransaction> result = await Approve(Guid.NewGuid());

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task ATooLongRejectionReason_IsRejected()
    {
        Transaction writeOff = PendingWriteOff();

        Result<ReviewedTransaction> result = await Reject(writeOff.Id, new string('x', 501));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal(TransactionStatus.Created, writeOff.Status);
    }
}
