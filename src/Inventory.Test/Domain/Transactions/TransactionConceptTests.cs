using Inventory.Domain.Transactions;
using Inventory.Domain.Transactions.Events;
using Joseco.DDD.Core.Results;

namespace Inventory.Test.Domain.Transactions;

/// <summary>R10/R11: the concept fixes the direction of the movement and the flow it may come from.</summary>
public class TransactionConceptTests
{
    private readonly TransactionFactory _factory = new();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _item = Guid.NewGuid();

    private static string CodeOf(Action create) => Assert.Throws<DomainException>(create).Error.Code;

    [Theory]
    [InlineData(TransactionConcept.Purchase, TransactionType.Entry)]
    [InlineData(TransactionConcept.Return, TransactionType.Entry)]
    [InlineData(TransactionConcept.OpeningBalance, TransactionType.Entry)]
    [InlineData(TransactionConcept.Sale, TransactionType.Exit)]
    [InlineData(TransactionConcept.Damaged, TransactionType.Exit)]
    [InlineData(TransactionConcept.Lost, TransactionType.Exit)]
    public void EachConcept_HasItsDirection(TransactionConcept concept, TransactionType type)
    {
        Assert.Equal(type, concept.TypeOf());
    }

    [Theory]
    [InlineData(TransactionConcept.OpeningBalance, true)]
    [InlineData(TransactionConcept.Damaged, true)]
    [InlineData(TransactionConcept.Lost, true)]
    [InlineData(TransactionConcept.Sale, false)]
    [InlineData(TransactionConcept.Purchase, false)]
    [InlineData(TransactionConcept.Return, false)]
    public void OnlyWriteOffsAndOpeningBalances_AreCreatedByHand(TransactionConcept concept, bool manual)
    {
        Assert.Equal(manual, concept.CanBeCreatedManually());
    }

    [Fact]
    public void ASale_ComesFromAnOrder()
    {
        Guid order = Guid.NewGuid();

        Transaction sale = _factory.CreateExitTransaction(_user, TransactionConcept.Sale, [(_item, 1)], sourceId: order, sourceType: TransactionSources.Orders);

        Assert.Equal(TransactionConcept.Sale, sale.Concept);
        Assert.False(sale.IsManual);
        Assert.Single(sale.DomainEvents.OfType<TransactionReserved>());
    }

    [Fact]
    public void ASale_WithoutAnOrderOrFromAnotherFlow_IsRejected()
    {
        Assert.Equal("Transaction.ConceptDoesNotMatchSource",
            CodeOf(() => _factory.CreateExitTransaction(_user, TransactionConcept.Sale, [(_item, 1)])));
        Assert.Equal("Transaction.ConceptDoesNotMatchSource",
            CodeOf(() => _factory.CreateExitTransaction(_user, TransactionConcept.Sale, [(_item, 1)], sourceId: Guid.NewGuid(), sourceType: TransactionSources.Purchasing)));
    }

    [Fact]
    public void PurchasesAndReturns_ComeFromTheirOwnFlow()
    {
        Transaction purchase = _factory.CreateEntryTransaction(_user, TransactionConcept.Purchase, [(_item, 1, 10m)],
            sourceId: Guid.NewGuid(), sourceType: TransactionSources.Purchasing);
        Transaction salesReturn = _factory.CreateEntryTransaction(_user, TransactionConcept.Return, [(_item, 1, 10m)],
            sourceId: Guid.NewGuid(), sourceType: TransactionSources.OrdersReturn);

        Assert.Equal(TransactionConcept.Purchase, purchase.Concept);
        Assert.Equal(TransactionConcept.Return, salesReturn.Concept);
        Assert.Equal("Transaction.ConceptDoesNotMatchSource",
            CodeOf(() => _factory.CreateEntryTransaction(_user, TransactionConcept.Purchase, [(_item, 1, 10m)])));
        Assert.Equal("Transaction.ConceptDoesNotMatchSource",
            CodeOf(() => _factory.CreateEntryTransaction(_user, TransactionConcept.Return, [(_item, 1, 10m)],
                sourceId: Guid.NewGuid(), sourceType: TransactionSources.Orders)));
    }

    [Fact]
    public void AConceptInTheWrongDirection_IsRejected()
    {
        Assert.Equal("Transaction.ConceptDoesNotMatchType",
            CodeOf(() => _factory.CreateEntryTransaction(_user, TransactionConcept.Damaged, [(_item, 1, 10m)], "broken")));
        Assert.Equal("Transaction.ConceptDoesNotMatchType",
            CodeOf(() => _factory.CreateExitTransaction(_user, TransactionConcept.OpeningBalance, [(_item, 1)])));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void AWriteOff_WithoutANote_IsRejected(string? notes)
    {
        Assert.Equal("Transaction.NotesRequired",
            CodeOf(() => _factory.CreateExitTransaction(_user, TransactionConcept.Damaged, [(_item, 1)], notes)));
        Assert.Equal("Transaction.NotesRequired",
            CodeOf(() => _factory.CreateExitTransaction(_user, TransactionConcept.Lost, [(_item, 1)], notes)));
    }

    [Fact]
    public void AWriteOff_CannotComeFromAnotherFlow()
    {
        Assert.Equal("Transaction.ConceptDoesNotMatchSource",
            CodeOf(() => _factory.CreateExitTransaction(_user, TransactionConcept.Damaged, [(_item, 1)], "broken",
                Guid.NewGuid(), TransactionSources.Orders)));
    }

    [Fact]
    public void ATooLongNote_IsRejected()
    {
        Assert.Equal("Transaction.NotesTooLong",
            CodeOf(() => _factory.CreateExitTransaction(_user, TransactionConcept.Lost, [(_item, 1)], new string('x', 501))));
    }

    [Fact]
    public void AWriteOff_ReservesTheStock_AndKeepsTheTrimmedNote()
    {
        Transaction writeOff = _factory.CreateExitTransaction(_user, TransactionConcept.Damaged, [(_item, 2)], "  screen broken in transit ");

        Assert.Equal(TransactionConcept.Damaged, writeOff.Concept);
        Assert.Equal("screen broken in transit", writeOff.Notes);
        Assert.True(writeOff.IsManual);
        TransactionItemReserved reserved = Assert.Single(writeOff.DomainEvents.OfType<TransactionItemReserved>());
        Assert.Equal(2, reserved.Quantity);
        Assert.Empty(writeOff.DomainEvents.OfType<TransactionReserved>()); // no sale is told anything
    }

    [Fact]
    public void AnOpeningBalance_NeedsNoNote()
    {
        Transaction opening = _factory.CreateEntryTransaction(_user, TransactionConcept.OpeningBalance, [(_item, 5, 100m)], "  ");

        Assert.Equal(TransactionType.Entry, opening.Type);
        Assert.Null(opening.Notes);
    }
}
