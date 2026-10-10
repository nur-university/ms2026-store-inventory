using Joseco.DDD.Core.Results;
using MediatR;

namespace Inventory.Application.Transactions.CreateTransaction
{
    /// <summary>
    /// A transaction created by hand (R11): a write-off (<c>Damaged</c>, <c>Lost</c>) or an <c>OpeningBalance</c>. It is
    /// created pending and is approved or rejected afterwards. The direction (entry/exit) follows from the concept.
    /// </summary>
    public record CreateTransactionCommand(Guid UserCreatorId, string Concept, ICollection<CreateTransacionItemCommand> Items, string? Notes = null)
        : IRequest<Result<Guid>>;

    /// <summary>The unitary cost is only used by entries; an exit leaves at the average cost of the item.</summary>
    public record CreateTransacionItemCommand(Guid ItemId, int Quantity, decimal UnitaryCost);
}
