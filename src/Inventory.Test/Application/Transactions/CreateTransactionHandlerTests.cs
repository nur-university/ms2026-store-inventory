using Inventory.Application.Items.EventHanders;
using Inventory.Application.Transactions.CreateTransaction;
using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Events;
using Inventory.Domain.Users;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Joseco.Outbox.Contracts.Model;
using Joseco.Outbox.Contracts.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Test.Application.Transactions;

public class CreateTransactionHandlerTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOutboxService<DomainEvent>> _outbox = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly CreateTransactionHandler _handler;
    private readonly OrderStarted _message;

    public CreateTransactionHandlerTests()
    {
        _handler = new CreateTransactionHandler(new TransactionFactory(), _transactions.Object, _unitOfWork.Object,
            Mock.Of<IItemRepository>(), NullLogger<CreateTransactionHandler>.Instance, _outbox.Object, _users.Object);
        _message = new OrderStarted(Guid.NewGuid(),
            [new OrderItemStarted { ProductId = Guid.NewGuid(), Price = 10, Quantity = 1 }]);
    }

    private void VerifyOutboxFailureSaved(Times times) =>
        _outbox.Verify(o => o.AddAsync(It.Is<OutboxMessage<DomainEvent>>(m => m.Content is ReservationTransactionFailed)), times);

    [Fact]
    public async Task OrderStarted_WhenTheOrderAlreadyHasAReservation_IsIgnored()
    {
        _transactions.Setup(t => t.GetBySourceAsync(_message.OrderId, "orders"))
            .ReturnsAsync(new TransactionFactory().CreateExitTransaction(Guid.NewGuid(), TransactionConcept.Sale, [(Guid.NewGuid(), 1)], sourceId: _message.OrderId, sourceType: "orders"));

        await _handler.HandleAsync(_message, CancellationToken.None);

        _transactions.Verify(t => t.AddAsync(It.IsAny<Transaction>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        VerifyOutboxFailureSaved(Times.Never());
    }

    [Fact]
    public async Task OrderStarted_WhenThereIsNotEnoughStock_RecordsAReservationFailure()
    {
        int commits = 0;
        // The first commit fails because stock is insufficient; the second one saves the failure message.
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ++commits == 1
                ? throw new DomainException(ItemErros.InsuficentStockToReserve(0, 1))
                : Task.CompletedTask);

        await _handler.HandleAsync(_message, CancellationToken.None);

        VerifyOutboxFailureSaved(Times.Once());
        Assert.Equal(2, commits);
    }

    [Fact]
    public async Task OrderStarted_WhenTheFailureIsNotABusinessRule_PropagatesSoTheMessageIsRetried()
    {
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("database unavailable"));

        await Assert.ThrowsAsync<TimeoutException>(() => _handler.HandleAsync(_message, CancellationToken.None));

        // It must NOT tell Orders the reservation failed: the stock may well be available.
        VerifyOutboxFailureSaved(Times.Never());
    }

    private static readonly Guid SystemUser = new("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    private Task<Guid> CreatorOfTheReservation(Guid? userOnTheOrder, bool inventoryKnowsTheUser)
    {
        Transaction? created = null;
        _transactions.Setup(t => t.AddAsync(It.IsAny<Transaction>())).Callback<Transaction>(t => created = t).Returns(Task.CompletedTask);
        if (userOnTheOrder.HasValue && inventoryKnowsTheUser)
        {
            _users.Setup(u => u.GetByIdAsync(userOnTheOrder.Value, true)).ReturnsAsync(User.Create(userOnTheOrder.Value, "Customer"));
        }

        OrderStarted message = new(Guid.NewGuid(), [new OrderItemStarted { ProductId = Guid.NewGuid(), Price = 10, Quantity = 1 }])
        {
            UserId = userOnTheOrder
        };

        return _handler.HandleAsync(message, CancellationToken.None).ContinueWith(_ => created!.CreatorId);
    }

    [Fact]
    public async Task OrderStarted_UsesTheCustomerOfTheOrderAsCreator()
    {
        Guid customer = Guid.NewGuid();

        Assert.Equal(customer, await CreatorOfTheReservation(customer, inventoryKnowsTheUser: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OrderStarted_FallsBackToTheSystemUser(bool orderHasUser)
    {
        Assert.Equal(SystemUser, await CreatorOfTheReservation(orderHasUser ? Guid.NewGuid() : null, inventoryKnowsTheUser: false));
    }

    [Fact]
    public async Task ReserveItem_ForAnUnknownItem_IsRejected()
    {
        Mock<IItemRepository> items = new();
        items.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), false)).ReturnsAsync((Item?)null);
        ReserveItemWhenTransactionItemReserved handler = new(items.Object);

        DomainException ex = await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new TransactionItemReserved(Guid.NewGuid(), 1, Guid.NewGuid()), CancellationToken.None));

        Assert.Equal("ItemNotFound", ex.Error.Code);
    }
}
