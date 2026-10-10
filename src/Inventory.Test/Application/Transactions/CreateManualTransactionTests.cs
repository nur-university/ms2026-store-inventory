using Inventory.Application.Transactions.CreateTransaction;
using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Inventory.Domain.Users;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Joseco.Outbox.Contracts.Service;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nur.Store2026.Integration.Orders;

namespace Inventory.Test.Application.Transactions;

/// <summary>R11: by hand only write-offs and opening balances are created; everything else comes from its own flow.</summary>
public class CreateManualTransactionTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IItemRepository> _items = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly CreateTransactionHandler _handler;
    private readonly Guid _tv = Guid.NewGuid();
    private readonly Guid _clerk = Guid.NewGuid();
    private Transaction? _added;

    private static readonly Guid SystemUser = new("3fa85f64-5717-4562-b3fc-2c963f66afa6");

    public CreateManualTransactionTests()
    {
        _handler = new CreateTransactionHandler(new TransactionFactory(), _transactions.Object, _unitOfWork.Object,
            _items.Object, NullLogger<CreateTransactionHandler>.Instance, Mock.Of<IOutboxService<DomainEvent>>(), _users.Object);
        _items.Setup(i => i.GetItemsFromId(It.IsAny<ICollection<Guid>>(), true))
            .ReturnsAsync((ICollection<Guid> ids, bool _) => ids.Where(id => id == _tv).Select(id => new Item(id, "Smart TV")).ToList());
        _users.Setup(u => u.GetByIdAsync(_clerk, true)).ReturnsAsync(User.Create(_clerk, "Clerk"));
        _transactions.Setup(t => t.AddAsync(It.IsAny<Transaction>())).Callback<Transaction>(t => _added = t).Returns(Task.CompletedTask);
    }

    private Task<Result<Guid>> Create(string concept, string? notes = "screen broken", params CreateTransacionItemCommand[] items) =>
        _handler.Handle(new CreateTransactionCommand(_clerk, concept,
            items.Length == 0 ? [new CreateTransacionItemCommand(_tv, 1, 0)] : items, notes), CancellationToken.None);

    private void VerifyNothingSaved()
    {
        _transactions.Verify(t => t.AddAsync(It.IsAny<Transaction>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Sale")]
    [InlineData("Purchase")]
    [InlineData("Return")]
    public async Task AConceptThatComesFromAFlow_CannotBeCreatedByHand(string concept)
    {
        Result<Guid> result = await Create(concept, items: new CreateTransacionItemCommand(_tv, 1, 10m));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Transaction.ConceptNotManual", result.Error.Code);
        VerifyNothingSaved();
    }

    [Theory]
    [InlineData("Stolen")]
    [InlineData("4")]
    [InlineData("")]
    public async Task AnUnknownConcept_IsRejected(string concept)
    {
        Result<Guid> result = await Create(concept);

        Assert.Equal("Transaction.InvalidConcept", result.Error.Code);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task AWriteOffWithoutANote_IsRejected()
    {
        Result<Guid> result = await Create("Damaged", notes: " ");

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("Transaction.NotesRequired", result.Error.Code);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheItems_AreValidated()
    {
        Assert.Equal("Transaction.NoItems",
            (await _handler.Handle(new CreateTransactionCommand(_clerk, "Lost", [], "gone"), CancellationToken.None)).Error.Code);
        Assert.Equal("Transaction.InvalidQuantity", (await Create("Lost", "gone", new CreateTransacionItemCommand(_tv, 0, 0))).Error.Code);
        Assert.Equal("Transaction.DuplicatedItem",
            (await Create("Lost", "gone", new CreateTransacionItemCommand(_tv, 1, 0), new CreateTransacionItemCommand(_tv, 2, 0))).Error.Code);
        Assert.Equal("Transaction.UnknownItem", (await Create("Lost", "gone", new CreateTransacionItemCommand(Guid.NewGuid(), 1, 0))).Error.Code);
        Assert.Equal("Transaction.InvalidCost", (await Create("OpeningBalance", null, new CreateTransacionItemCommand(_tv, 5, 0))).Error.Code);
        VerifyNothingSaved();
    }

    [Fact]
    public async Task AWriteOff_IsCreatedPending_ByTheAuthenticatedUser()
    {
        Result<Guid> result = await Create("damaged", "  screen broken  ", new CreateTransacionItemCommand(_tv, 2, 999m));

        Assert.True(result.IsSuccess);
        Assert.NotNull(_added);
        Assert.Equal(result.Value, _added!.Id);
        Assert.Equal(TransactionType.Exit, _added.Type);
        Assert.Equal(TransactionConcept.Damaged, _added.Concept);
        Assert.Equal(TransactionStatus.Created, _added.Status);
        Assert.Equal("screen broken", _added.Notes);
        Assert.Equal(_clerk, _added.CreatorId);
        Assert.Equal(0m, _added.Items.Single().UnitaryCost.Value); // an exit is not valued at the cost the client sends
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnOpeningBalance_IsAnEntryAtTheGivenCost()
    {
        Result<Guid> result = await Create("OpeningBalance", null, new CreateTransacionItemCommand(_tv, 5, 120m));

        Assert.True(result.IsSuccess);
        Assert.Equal(TransactionType.Entry, _added!.Type);
        Assert.Equal(600m, _added.TotalCost.Value);
    }

    [Fact]
    public async Task NotEnoughAvailableStock_ForAWriteOff_IsABusinessError()
    {
        _unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DomainException(ItemErros.InsuficentStockToReserve(0, 1)));

        Result<Guid> result = await Create("Lost", "gone");

        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Equal("InsuficentStockToReserve", result.Error.Code);
    }

    [Fact]
    public async Task AUserInventoryDoesNotKnow_IsRecordedAsTheSystemUser()
    {
        Result<Guid> result = await _handler.Handle(
            new CreateTransactionCommand(Guid.NewGuid(), "Lost", [new CreateTransacionItemCommand(_tv, 1, 0)], "gone"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(SystemUser, _added!.CreatorId);
    }

    [Fact]
    public async Task OrderStarted_CreatesASaleFromTheOrder()
    {
        Guid order = Guid.NewGuid();

        await _handler.HandleAsync(new OrderStarted(order, [new OrderItemStarted { ProductId = _tv, Price = 10, Quantity = 1 }]),
            CancellationToken.None);

        Assert.Equal(TransactionConcept.Sale, _added!.Concept);
        Assert.Equal(order, _added.SourceId);
        Assert.Equal(TransactionSources.Orders, _added.SourceType);
    }
}
