using Joseco.DDD.Core.Results;

namespace Inventory.Domain.Transactions;

public static class TransactionErrors
{
    public static Error NotReleased(Guid transactionId) =>
        new("Transaction.NotReleased",
            $"The transaction {transactionId} belongs to a sale that has not been paid yet, so the goods cannot be delivered.",
            ErrorType.Conflict);

    public static Error NotFound(Guid transactionId) =>
        new("Transaction.NotFound", $"The transaction {transactionId} does not exist.", ErrorType.NotFound);

    public static Error NotFoundForSource(Guid sourceId, string sourceType) =>
        new("Transaction.NotFound", $"There is no stock reservation for {sourceType} {sourceId}.", ErrorType.NotFound);

    public static Error NotFoundForInvoice(string invoiceNumber) =>
        new("Transaction.InvoiceNotFound", $"There is no stock reservation for the invoice {invoiceNumber}.", ErrorType.NotFound);

    public static Error NotPending(Guid transactionId, TransactionStatus status) =>
        new("Transaction.NotPending", $"The transaction {transactionId} is {status}, so it cannot be delivered.", ErrorType.Conflict);

    // ---- concepts (R10, R11)

    public static Error InvalidConcept(string? concept) =>
        new("Transaction.InvalidConcept",
            $"'{concept}' is not a transaction concept. Use one of: {string.Join(", ", Enum.GetNames<TransactionConcept>())}.",
            ErrorType.Validation);

    public static Error ConceptNotManual(TransactionConcept concept) =>
        new("Transaction.ConceptNotManual",
            $"A {concept} transaction only comes from its own flow; by hand only {TransactionConcept.OpeningBalance}, {TransactionConcept.Damaged} and {TransactionConcept.Lost} can be created.",
            ErrorType.Validation);

    public static Error ConceptDoesNotMatchType(TransactionConcept concept, TransactionType type) =>
        new("Transaction.ConceptDoesNotMatchType", $"A {concept} transaction is an {concept.TypeOf()}, not an {type}.", ErrorType.Validation);

    public static Error ConceptDoesNotMatchSource(TransactionConcept concept, string? sourceType) =>
        new("Transaction.ConceptDoesNotMatchSource",
            concept.SourceTypeOf() is { } expected
                ? $"A {concept} transaction must come from '{expected}', not from '{sourceType ?? "nowhere"}'."
                : $"A {concept} transaction is created by hand and cannot come from '{sourceType}'.",
            ErrorType.Validation);

    public static Error NotesRequired(TransactionConcept concept) =>
        new("Transaction.NotesRequired", $"A {concept} transaction needs a note explaining what happened to the goods.", ErrorType.Validation);

    public static Error NotesTooLong() =>
        new("Transaction.NotesTooLong", $"The note cannot be longer than {TransactionConceptRules.MaxNotesLength} characters.", ErrorType.Validation);

    // ---- items of a transaction created by hand

    public static Error NoItems() =>
        new("Transaction.NoItems", "The transaction needs at least one item.", ErrorType.Validation);

    public static Error InvalidQuantity(Guid itemId) =>
        new("Transaction.InvalidQuantity", $"The quantity of the item {itemId} must be greater than zero.", ErrorType.Validation);

    public static Error InvalidCost(Guid itemId) =>
        new("Transaction.InvalidCost", $"The unitary cost of the item {itemId} must be greater than zero.", ErrorType.Validation);

    public static Error DuplicatedItem(Guid itemId) =>
        new("Transaction.DuplicatedItem", $"The item {itemId} appears more than once.", ErrorType.Validation);

    public static Error UnknownItem(Guid itemId) =>
        new("Transaction.UnknownItem", $"The item {itemId} does not exist in the inventory.", ErrorType.Validation);

    // ---- review of a transaction created by hand (R11, 5.2)

    public static Error NotManual(Guid transactionId) =>
        new("Transaction.NotManual",
            $"The transaction {transactionId} comes from another flow (a sale, a purchase or a return) and is closed by it, not approved or rejected by hand.",
            ErrorType.Conflict);

    public static Error AlreadyReviewed(Guid transactionId, TransactionStatus status) =>
        new("Transaction.AlreadyReviewed", $"The transaction {transactionId} is already {status}.", ErrorType.Conflict);
}
