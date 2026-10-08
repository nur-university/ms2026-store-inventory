using Inventory.Domain.Items;
using Inventory.Domain.Shared;
using Joseco.DDD.Core.Results;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Inventory.Test.Domain.Items
{
    public class ItemTest
    {
        [Fact]
        public void ItemCreationIsValid()
        {
            // Arrange
            string name = "Item 1";
            Guid itemId = Guid.NewGuid();
            // Act
            Item item = new Item(itemId, name);
            // Assert
            Assert.Equal(name, item.Name);
            Assert.Equal<QuantityValue>(0, item.Stock);
            Assert.Equal(0, item.UnitaryCost);
            Assert.Equal(itemId, item.Id);
        }
        [Fact]
        public void UpdateStockIsValidWithOneElement()
        {
            int qtyToAdd = 10;
            decimal unitaryCost = 100;
            string name = "Item 1";
            Guid itemId = Guid.NewGuid();
            Item item = new Item(itemId, name);
            CostValue expectedCost = 100;

            item.AddStock(qtyToAdd, unitaryCost);

            Assert.Equal<QuantityValue>(qtyToAdd, item.Stock);
            Assert.Equal(expectedCost, item.UnitaryCost);
        }
        [Fact]
        public void UpdateStockIsValidWithMultipleElements()
        {
            int qtyToAdd1 = 10;
            decimal unitaryCost1 = 100;
            int qtyToAdd2 = 20;
            decimal unitaryCost2 = 200;
            int qtyToAdd3 = 30;
            decimal unitaryCost3 = 300;
            string name = "Item 1";
            Guid itemId = Guid.NewGuid();
            Item item = new Item(itemId, name);
            CostValue expectedCost = 233.34m;
            int expextedQty = 60;

            item.AddStock(qtyToAdd1, unitaryCost1);
            item.AddStock(qtyToAdd2, unitaryCost2);
            item.AddStock(qtyToAdd3, unitaryCost3);

            Assert.Equal<QuantityValue>(expextedQty, item.Stock);
            Assert.Equal(expectedCost, item.UnitaryCost);
        }
        [Theory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(-1, -1, 0, 0)]
        public void UpdateStockWithValues(int qtyToAdd, decimal unitaryCost, int expectedQty, decimal expectedCost) {
            
            string name = "Item 1";
            Guid itemId = Guid.NewGuid();
            Item item = new Item(itemId, name);

            Assert.Throws<DomainException>(() =>
            {
                item.AddStock(qtyToAdd, unitaryCost);
            });
        }
    }
}
