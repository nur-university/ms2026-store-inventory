namespace Inventory.Domain.Transactions;

/// <summary>Why the stock goes in or out. Transactions created before concepts existed have none.</summary>
public enum TransactionConcept
{
    // entries
    Purchase,
    Return,
    OpeningBalance,

    // exits
    Sale,
    Damaged,
    Lost
}

/// <summary>The flows a transaction can come from (its <c>SourceType</c>).</summary>
public static class TransactionSources
{
    /// <summary>The stock reserved for a sales note; the source id is the order id.</summary>
    public const string Orders = "orders";

    /// <summary>The goods received for a purchase order.</summary>
    public const string Purchasing = "purchasing";

    /// <summary>The goods a customer brings back; the source id is the sales return id.</summary>
    public const string OrdersReturn = "orders.return";
}

public static class TransactionConceptRules
{
    public const int MaxNotesLength = 500;

    public static TransactionType TypeOf(this TransactionConcept concept) =>
        concept is TransactionConcept.Purchase or TransactionConcept.Return or TransactionConcept.OpeningBalance
            ? TransactionType.Entry
            : TransactionType.Exit;

    /// <summary>Purchases, returns and sales only come from their own flow, so they always have a supporting document.</summary>
    public static bool CanBeCreatedManually(this TransactionConcept concept) =>
        concept is TransactionConcept.OpeningBalance or TransactionConcept.Damaged or TransactionConcept.Lost;

    /// <summary>A write-off must say what happened to the goods.</summary>
    public static bool RequiresNotes(this TransactionConcept concept) =>
        concept is TransactionConcept.Damaged or TransactionConcept.Lost;

    /// <summary>The only source a concept that comes from a flow may have; null for the manual ones.</summary>
    public static string? SourceTypeOf(this TransactionConcept concept) => concept switch
    {
        TransactionConcept.Sale => TransactionSources.Orders,
        TransactionConcept.Purchase => TransactionSources.Purchasing,
        TransactionConcept.Return => TransactionSources.OrdersReturn,
        _ => null
    };
}
