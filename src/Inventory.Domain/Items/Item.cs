
using Inventory.Domain.Shared;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Domain.Items;

public class Item : AggregateRoot
{

    public string Name { get; private set; }
    public QuantityValue Stock { get; private set; }
    public QuantityValue Reserved { get; private set; }
    public QuantityValue Available { get; set; }
    public CostValue UnitaryCost { get; private set; }

    public Item(Guid id, string name) : base(id)
    {
        Name = name;
        Stock = 0;
        UnitaryCost = 0;
        Reserved = 0;
        Available = 0;
    }

    /// <summary>
    /// Adds stock to the item. If the item already has stock, it will calculate the new unitary cost based on the average cost of the items in stock and the new items being added.
    /// </summary>
    /// <param name="quantityToAdd">Quantity of the Stock to add</param>
    /// <param name="unitaryCost">Unitary cost of new stock added</param>
    /// <exception cref="DomainException"></exception>
    public void AddStock(int quantityToAdd, decimal unitaryCost)
    {
        if (quantityToAdd <= 0)
        {
            throw new DomainException(ItemErros.NonNegativeStock());
        }
        // Calculate the new unitary cost based on the average cost of the items in stock and the new items being added
        int totalStock = Stock + quantityToAdd;
        CostValue newCost = Math.Round(
            (UnitaryCost * Stock + unitaryCost * quantityToAdd) / totalStock, 2);
        UnitaryCost = newCost;

        Stock = totalStock;
        Available = Stock - Reserved;
    }

    public void ApplyReservation(int quantityToSubstract)
    {
        if (quantityToSubstract > Reserved)
        {
            throw new DomainException(ItemErros.InsuficentStockToSubstract(Reserved, quantityToSubstract));
        }
        Reserved -= quantityToSubstract;
        Stock -= quantityToSubstract;
        Available = Stock - Reserved;
    }

    public void ReserveStock(int quantityToReserve)
    {
        if (quantityToReserve > Available)
        {
            throw new DomainException(ItemErros.InsuficentStockToReserve(Stock, quantityToReserve));
        }
        Reserved += quantityToReserve;
        Available -= quantityToReserve;
    }

    public void UnreserveStock(int quantityToUnreserve)
    {
        if (quantityToUnreserve > Reserved)
        {
            throw new DomainException(ItemErros.InsuficentStockToSubstract(Reserved, quantityToUnreserve));
        }
        Reserved -= quantityToUnreserve;
        Available += quantityToUnreserve;
    }

    //Need for EF Core
    private Item() { }
}
