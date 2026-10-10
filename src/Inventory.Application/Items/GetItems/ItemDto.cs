namespace Inventory.Application.Items.GetItems
{
    public class ItemDto
    {
        public Guid Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Stock { get; set; }
        public int Reserved { get; set; }
        public int Available { get; set; }

        /// <summary>Average cost of the units in stock.</summary>
        public decimal UnitaryCost { get; set; }
    }
}
