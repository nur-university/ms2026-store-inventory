using Joseco.DDD.Core.Results;

namespace Inventory.Domain.Transactions;

public static class TransactionErrors
{
    public static Error NotReleased(Guid transactionId) =>
        new("Transaction.NotReleased",
            $"The transaction {transactionId} belongs to a sale that has not been paid yet, so the goods cannot be delivered.",
            ErrorType.Conflict);

    public static Error NotFoundForSource(Guid sourceId, string sourceType) =>
        new("Transaction.NotFound", $"There is no stock reservation for {sourceType} {sourceId}.", ErrorType.NotFound);

    public static Error NotFoundForInvoice(string invoiceNumber) =>
        new("Transaction.InvoiceNotFound", $"There is no stock reservation for the invoice {invoiceNumber}.", ErrorType.NotFound);

    public static Error NotPending(Guid transactionId, TransactionStatus status) =>
        new("Transaction.NotPending", $"The transaction {transactionId} is {status}, so it cannot be delivered.", ErrorType.Conflict);
}
