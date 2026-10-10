namespace Inventory.Application.Transactions.GetTransaccionById
{
    /// <summary>A transaction with its lines. An exit line has no cost: it leaves at the average cost of the item.</summary>
    public class TransactionDto
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string? Concept { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime? CancelDate { get; set; }
        public Guid CreatorId { get; set; }
        public string? CreatorName { get; set; }
        public Guid? SourceId { get; set; }
        public string? SourceType { get; set; }
        public string? Notes { get; set; }
        public Guid? ReviewedBy { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? ReleasedOn { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerDocument { get; set; }
        public decimal TotalCost { get; set; }
        public List<TransactionLineDto> Lines { get; set; } = [];
    }

    public class TransactionLineDto
    {
        public Guid ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitaryCost { get; set; }
        public decimal SubTotal { get; set; }
    }
}
