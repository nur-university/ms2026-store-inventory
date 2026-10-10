using Inventory.Application.Items.GetItem;
using Inventory.Application.Items.GetItemMovements;
using Inventory.Application.Transactions.GetTransaccionById;
using Inventory.Application.Transactions.GetTransactions;
using Inventory.Infrastructure.Persistence.StoredModel;
using Inventory.Infrastructure.Persistence.StoredModel.Entities;
using Inventory.Infrastructure.Queries.Item;
using Inventory.Infrastructure.Queries.Transaction;
using Joseco.DDD.Core.Results;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Test.Infrastructure;

/// <summary>5.3: the inventory queries against the real read model (SQLite in memory).</summary>
public sealed class InventoryQueriesTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly StoredDbContext _db;
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _tv = Guid.NewGuid();
    private readonly Guid _fridge = Guid.NewGuid();
    private static readonly DateTime Day1 = new(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);

    public InventoryQueriesTests()
    {
        _connection.Open();
        _db = new StoredDbContext(new DbContextOptionsBuilder<StoredDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        _db.User.Add(new UserStoredModel { Id = _user, FullName = "Ana Manager" });
        _db.Item.Add(new ItemStoredModel { Id = _tv, ItemName = "Smart TV", Stock = 12, Reserverd = 1, Available = 11, UnitaryCost = 100m });
        _db.Item.Add(new ItemStoredModel { Id = _fridge, ItemName = "Fridge", Stock = 0, Reserverd = 0, Available = 0, UnitaryCost = 0m });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Guid Add(string type, string? concept, string status, DateTime created, int quantity, Guid? item = null, decimal cost = 0m,
        string? sourceType = null, string? notes = null, DateTime? completed = null)
    {
        Guid id = Guid.NewGuid();
        _db.Transaction.Add(new TransactionStoredModel
        {
            Id = id,
            UserCreatorId = _user,
            TransactionType = type,
            Concept = concept,
            Status = status,
            CreationDate = created,
            CompletedDate = status == "Completed" ? completed ?? created.AddHours(1) : null,
            TotalCost = quantity * cost,
            SourceType = sourceType,
            SourceId = sourceType == null ? null : Guid.NewGuid(),
            Notes = notes,
            Items =
            [
                new TransactionItemStoredModel
                {
                    Id = Guid.NewGuid(), ItemId = item ?? _tv, TransactionId = id, Quantity = quantity, UnitaryCost = cost, SubTotal = quantity * cost
                }
            ]
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return id;
    }

    /// <summary>The history of the TV: 10 in, 2 sold, 1 damaged, 5 in, 1 lost pending, 3 sold canceled.</summary>
    private (Guid opening, Guid sale, Guid damaged, Guid entry, Guid pendingLost) TvHistory()
    {
        Guid opening = Add("Entry", null, "Completed", Day1, 10, cost: 100m);
        Guid sale = Add("Exit", "Sale", "Completed", Day1.AddDays(1), 2, sourceType: "orders");
        Guid damaged = Add("Exit", "Damaged", "Completed", Day1.AddDays(2), 1, notes: "screen broken");
        Guid entry = Add("Entry", "OpeningBalance", "Completed", Day1.AddDays(3), 5, cost: 100m);
        Guid pendingLost = Add("Exit", "Lost", "Created", Day1.AddDays(4), 1, notes: "not found");
        Add("Exit", "Sale", "Canceled", Day1.AddDays(5), 3, sourceType: "orders");
        return (opening, sale, damaged, entry, pendingLost);
    }

    private Task<Result<PagedTransactions>> List(GetTransactionsQuery query) =>
        new GetTransactionsHandler(_db).Handle(query, CancellationToken.None);

    private Task<Result<ItemKardexDto>> Kardex(Guid item, DateTime? from = null, DateTime? to = null, int page = 1, int pageSize = 50) =>
        new GetItemMovementsHandler(_db).Handle(new GetItemMovementsQuery(item, from, to, page, pageSize), CancellationToken.None);

    // ---- GET api/transaction

    [Fact]
    public async Task Transactions_AreListedNewestFirst()
    {
        TvHistory();

        PagedTransactions all = (await List(new GetTransactionsQuery())).Value;

        Assert.Equal(6, all.Total);
        Assert.Equal(all.Items.OrderByDescending(i => i.CreationDate).Select(i => i.Id), all.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Transactions_AreFilteredByTypeConceptStatusAndSource()
    {
        var history = TvHistory();

        Assert.Equal(2, (await List(new GetTransactionsQuery(Type: "entry"))).Value.Total);
        Assert.Equal([history.damaged], (await List(new GetTransactionsQuery(Concept: "Damaged"))).Value.Items.Select(i => i.Id));
        Assert.Equal([history.pendingLost], (await List(new GetTransactionsQuery(Status: "Created"))).Value.Items.Select(i => i.Id));
        Assert.Equal(2, (await List(new GetTransactionsQuery(SourceType: "orders"))).Value.Total);
        Assert.Equal(1, (await List(new GetTransactionsQuery(Type: "Exit", Concept: "Sale", Status: "Completed"))).Value.Total);

        TransactionSummaryDto damaged = (await List(new GetTransactionsQuery(Concept: "Damaged"))).Value.Items.Single();
        Assert.Equal("screen broken", damaged.Notes);
        Assert.Equal(1, damaged.ItemCount);
        Assert.Equal(1, damaged.TotalQuantity);
    }

    [Fact]
    public async Task Transactions_AreFilteredByWholeDays()
    {
        TvHistory();

        // Day1 + 1 and Day1 + 2: the 'to' date includes the whole day.
        PagedTransactions range = (await List(new GetTransactionsQuery(From: Day1.Date.AddDays(1), To: Day1.Date.AddDays(2)))).Value;

        Assert.Equal(2, range.Total);
    }

    [Fact]
    public async Task Transactions_ArePaged()
    {
        TvHistory();

        PagedTransactions second = (await List(new GetTransactionsQuery(Page: 2, PageSize: 4))).Value;

        Assert.Equal(6, second.Total);
        Assert.Equal(2, second.Items.Count);
        Assert.Equal(2, second.Page);
    }

    [Theory]
    [InlineData("Transfer", null, null)]
    [InlineData(null, "Stolen", null)]
    [InlineData(null, null, "1")]
    public async Task AnInvalidFilter_IsRejected(string? type, string? concept, string? status)
    {
        Result<PagedTransactions> result = await List(new GetTransactionsQuery(type, concept, status));

        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    [Fact]
    public async Task AnInvertedDateRange_IsRejected()
    {
        Result<PagedTransactions> result = await List(new GetTransactionsQuery(From: Day1.AddDays(3), To: Day1));

        Assert.Equal("Query.InvalidDateRange", result.Error.Code);
    }

    // ---- GET api/transaction/{id}

    [Fact]
    public async Task ATransaction_ShowsItsConceptNotesAndLines()
    {
        Guid damaged = TvHistory().damaged;

        TransactionDto? dto = await new GetTransactionByIdHandler(_db).Handle(new GetTransaccionByIdQuery(damaged), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("Damaged", dto!.Concept);
        Assert.Equal("Completed", dto.Status);
        Assert.Equal("screen broken", dto.Notes);
        Assert.Equal("Ana Manager", dto.CreatorName);
        Assert.NotNull(dto.CompletedDate);
        Assert.Null(dto.CancelDate);
        TransactionLineDto line = Assert.Single(dto.Lines);
        Assert.Equal("Smart TV", line.ItemName);
        Assert.Equal(1, line.Quantity);
    }

    [Fact]
    public async Task AnUnknownTransaction_IsNull()
    {
        Assert.Null(await new GetTransactionByIdHandler(_db).Handle(new GetTransaccionByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }

    // ---- GET api/item/{id}

    [Fact]
    public async Task AnItem_ShowsItsStock()
    {
        GetItemsHandler handler = new(_db);

        var tv = await handler.Handle(new GetItemQuery(_tv), CancellationToken.None);
        var unknown = await handler.Handle(new GetItemQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal((12, 1, 11, 100m), (tv!.Stock, tv.Reserved, tv.Available, tv.UnitaryCost));
        Assert.Null(unknown);
    }

    // ---- GET api/item/{id}/movements

    [Fact]
    public async Task TheKardex_ListsCompletedMovementsWithTheRunningBalance()
    {
        var history = TvHistory();

        ItemKardexDto kardex = (await Kardex(_tv)).Value;

        Assert.Equal(4, kardex.Total); // the pending write-off and the canceled sale did not move the stock
        Assert.Equal([history.opening, history.sale, history.damaged, history.entry], kardex.Movements.Select(m => m.TransactionId));
        Assert.Equal([10, 8, 7, 12], kardex.Movements.Select(m => m.Balance));
        Assert.Equal([10, 0, 0, 5], kardex.Movements.Select(m => m.QuantityIn));
        Assert.Equal([0, 2, 1, 0], kardex.Movements.Select(m => m.QuantityOut));
        Assert.Equal([100m, null, null, 100m], kardex.Movements.Select(m => m.UnitaryCost));
        Assert.Equal(0, kardex.BalanceBefore);
        Assert.Equal(12, kardex.BalanceAfter);
        Assert.Equal(kardex.Stock, kardex.BalanceAfter);
        Assert.Equal(1, kardex.Reserved);
        Assert.Equal("Damaged", kardex.Movements[2].Concept);
    }

    [Fact]
    public async Task TheKardex_KeepsTheBalanceAcrossPages()
    {
        TvHistory();

        ItemKardexDto second = (await Kardex(_tv, page: 2, pageSize: 2)).Value;

        Assert.Equal(8, second.BalanceBefore);
        Assert.Equal([7, 12], second.Movements.Select(m => m.Balance));
        Assert.Equal(12, second.BalanceAfter);
    }

    [Fact]
    public async Task TheKardex_BetweenTwoDates_StartsFromTheEarlierBalance()
    {
        TvHistory();

        // Completed on Day1+2 (damaged) only: the TV had 8 units before.
        ItemKardexDto range = (await Kardex(_tv, from: Day1.Date.AddDays(2), to: Day1.Date.AddDays(2))).Value;

        Assert.Equal(8, range.BalanceBefore);
        ItemMovementDto damaged = Assert.Single(range.Movements);
        Assert.Equal(7, damaged.Balance);
    }

    [Fact]
    public async Task TheKardex_IsOrderedByCompletionDate()
    {
        // Created first but completed last: an opening balance approved days later.
        Guid lateApproval = Add("Entry", "OpeningBalance", "Completed", Day1, 4, cost: 50m, completed: Day1.AddDays(9));
        Guid sale = Add("Exit", "Sale", "Completed", Day1.AddDays(1), 0, sourceType: "orders");

        ItemKardexDto kardex = (await Kardex(_tv)).Value;

        Assert.Equal([sale, lateApproval], kardex.Movements.Select(m => m.TransactionId));
    }

    [Fact]
    public async Task TheKardex_OfAnItemWithoutMovements_IsEmpty_AndOfAnUnknownItem_IsNotFound()
    {
        TvHistory();

        ItemKardexDto fridge = (await Kardex(_fridge)).Value;
        Result<ItemKardexDto> unknown = await Kardex(Guid.NewGuid());

        Assert.Empty(fridge.Movements);
        Assert.Equal(0, fridge.BalanceAfter);
        Assert.Equal(ErrorType.NotFound, unknown.Error.Type);
    }
}
