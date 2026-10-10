using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Inventory.Infrastructure.Persistence.StoredModel.Entities;

[Table("transaction")]
internal class TransactionStoredModel 
{
    [Key]
    [Column("transactionId")]
    public Guid Id { get; set; }

    [Required]
    [Column("userCreatorId")]
    public Guid UserCreatorId { get; set; }

    public UserStoredModel UserCreator { get; set; }

    [Required]
    [Column("transactionType")]
    [MaxLength(25)]
    public string TransactionType { get; set; }

    [Required]
    [Column("creationDate")]
    public DateTime CreationDate { get; set; }

    [Column("completedDate")]
    public DateTime? CompletedDate { get; set; }

    [Column("cancelDate")]
    public DateTime? CancelDate { get; set; }

    [Required]
    [Column("totalCost", TypeName = "decimal(18,2)")]
    public decimal TotalCost { get; set; }

    [Required]
    [Column("status")]
    [MaxLength(25)]
    public string Status { get; set; }


    [Column("sourceType")]
    [MaxLength(25)]
    public string? SourceType { get; set; }

    [Column("sourceId")]
    public Guid? SourceId { get; set; }

    [Column("releasedOn")]
    public DateTime? ReleasedOn { get; set; }

    [Column("invoiceNumber")]
    [MaxLength(20)]
    public string? InvoiceNumber { get; set; }

    [Column("customerName")]
    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [Column("customerDocument")]
    [MaxLength(50)]
    public string? CustomerDocument { get; set; }

    /// <summary>TransactionConcept; null for transactions created before concepts existed.</summary>
    [Column("concept")]
    [MaxLength(25)]
    public string? Concept { get; set; }

    [Column("notes")]
    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>Who approved or rejected a transaction created by hand (a user id from the token, not a foreign key).</summary>
    [Column("reviewedBy")]
    public Guid? ReviewedBy { get; set; }

    [Column("rejectionReason")]
    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public List<TransactionItemStoredModel> Items { get; set; }

}
