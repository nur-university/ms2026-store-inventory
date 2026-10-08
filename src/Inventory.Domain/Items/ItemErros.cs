using Joseco.DDD.Core.Results;

namespace Inventory.Domain.Items;

public static class ItemErros
{
    public static Error InsuficentStockToSubstract(int currentStock, int requestedStock)
    {
        return new Error("InsuficentStockToSubstract", $"The current stock is {currentStock}, it cannot substract {requestedStock}", ErrorType.Validation);
    }

    public static Error InsuficentStockToReserve(int availableStock, int requestedStock)
    {
        return new Error("InsuficentStockToReserve", "The available stock is {availableStock}, it cannot reserve {requestedStock}", ErrorType.Validation, availableStock, requestedStock);
    }

    public static Error ItemNotFound(Guid itemId) =>
        new Error("ItemNotFound", $"The item {itemId} does not exist", ErrorType.NotFound);

    public static Error NonNegativeStock() =>
        new Error("NonNegativeStock", "The value for cannot be negative", ErrorType.Validation);

}
