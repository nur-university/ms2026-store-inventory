using MediatR;

namespace Inventory.Application.Transactions.GetTransaccionById;

/// <summary>Null when the transaction does not exist.</summary>
public record GetTransaccionByIdQuery(Guid TransactionId) : IRequest<TransactionDto?>;
