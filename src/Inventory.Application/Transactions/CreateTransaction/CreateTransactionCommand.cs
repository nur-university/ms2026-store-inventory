using MediatR;

namespace Inventory.Application.Transactions.CreateTransaction
{
    public record CreateTransactionCommand(Guid UserCreatorId, string Type, ICollection<CreateTransacionItemCommand> Items, Guid? SourceId = null, string? SourceType = null) : IRequest<Guid>;

    public record CreateTransacionItemCommand(Guid ItemId, int Quantity, decimal UnitaryCost);
}
