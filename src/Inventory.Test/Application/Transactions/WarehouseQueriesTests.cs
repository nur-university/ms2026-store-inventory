using Inventory.Application.Transactions.DeliverOrder;
using Inventory.Domain.Items;
using Inventory.Domain.Transactions;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Moq;

namespace Inventory.Test.Application.Transactions;

/// <summary>What the warehouse sees: the sales to prepare, and the lookup and delivery by the invoice the customer shows.</summary>
public class WarehouseQueriesTests
{
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IItemRepository> _items = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _orderId = Guid.NewGuid();
    private readonly Guid _itemId = Guid.NewGuid();

    private Transaction PaidSale()
    {
        Transaction transaction = new TransactionFactory().CreateExitTransaction(Guid.NewGuid(), TransactionConcept.Sale, [(_itemId, 2)], sourceId: _orderId, sourceType: "orders");
        transaction.Release("F-00000042", "Ana Perez", "1234567");
        transaction.ClearDomainEvents();

        _transactions.Setup(t => t.GetByInvoiceAsync("F-00000042")).ReturnsAsync(transaction);
        _transactions.Setup(t => t.GetBySourceAsync(_orderId, "orders")).ReturnsAsync(transaction);
        _items.Setup(i => i.GetItemsFromId(It.IsAny<ICollection<Guid>>(), true))
            .ReturnsAsync(new List<Item> { new(_itemId, "Smart TV") });
        return transaction;
    }

    [Fact]
    public void Release_KeepsTheCustomerForTheClerk()
    {
        Transaction transaction = new TransactionFactory().CreateExitTransaction(Guid.NewGuid(), TransactionConcept.Sale, [(_itemId, 1)], sourceId: _orderId, sourceType: "orders");

        transaction.Release("F-1", "Ana Perez", "1234567");

        Assert.Equal("Ana Perez", transaction.CustomerName);
        Assert.Equal("1234567", transaction.CustomerDocument);
    }

    [Fact]
    public async Task ByInvoice_ShowsWhatTheCustomerIsEntitledToAndWhoTheyAre()
    {
        PaidSale();

        Result<PendingExitDto> result = await new GetExitByInvoiceHandler(_transactions.Object, _items.Object)
            .Handle(new GetExitByInvoiceQuery(" F-00000042 "), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Paid);
        Assert.Equal("Ana Perez", result.Value.CustomerName);
        Assert.Equal(_orderId, result.Value.OrderId);
        PendingExitLineDto line = Assert.Single(result.Value.Lines);
        Assert.Equal("Smart TV", line.ItemName);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task ByInvoice_UnknownNumber_IsNotFound()
    {
        _transactions.Setup(t => t.GetByInvoiceAsync(It.IsAny<string>())).ReturnsAsync((Transaction?)null);

        Result<PendingExitDto> result = await new GetExitByInvoiceHandler(_transactions.Object, _items.Object)
            .Handle(new GetExitByInvoiceQuery("F-99999999"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
    }

    [Fact]
    public async Task PendingExits_ListsTheSalesToPrepare_WithTheirPaymentStatus()
    {
        Transaction unpaid = new TransactionFactory().CreateExitTransaction(Guid.NewGuid(), TransactionConcept.Sale, [(_itemId, 1)], sourceId: Guid.NewGuid(), sourceType: "orders");
        Transaction paid = PaidSale();
        _transactions.Setup(t => t.GetPendingExitsAsync(0, 20))
            .ReturnsAsync((new List<Transaction> { unpaid, paid }, 2));

        Result<PagedPendingExits> result = await new GetPendingExitsHandler(_transactions.Object, _items.Object)
            .Handle(new GetPendingExitsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Value.Total);
        Assert.Equal([false, true], result.Value.Items.Select(i => i.Paid));
    }

    [Fact]
    public async Task DeliverByInvoice_DelegatesToTheDeliveryOfTheOrder()
    {
        PaidSale();
        Mock<IMediator> mediator = new();
        mediator.Setup(m => m.Send(It.IsAny<DeliverOrderCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new DeliveredOrder(_orderId, Guid.NewGuid(), "F-00000042", DateTime.UtcNow)));

        Result<DeliveredOrder> result = await new DeliverInvoiceHandler(_transactions.Object, mediator.Object)
            .Handle(new DeliverInvoiceCommand("F-00000042"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        mediator.Verify(m => m.Send(It.Is<DeliverOrderCommand>(c => c.OrderId == _orderId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeliverByInvoice_UnknownNumber_DeliversNothing()
    {
        _transactions.Setup(t => t.GetByInvoiceAsync(It.IsAny<string>())).ReturnsAsync((Transaction?)null);
        Mock<IMediator> mediator = new();

        Result<DeliveredOrder> result = await new DeliverInvoiceHandler(_transactions.Object, mediator.Object)
            .Handle(new DeliverInvoiceCommand("F-99999999"), CancellationToken.None);

        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        mediator.Verify(m => m.Send(It.IsAny<IRequest<Result<DeliveredOrder>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
