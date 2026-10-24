using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Models;

public class LedgerJournal
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string JournalNumber { get; set; } = string.Empty; // e.g. "JRN-20261022-0001"

    [Required]
    [MaxLength(64)]
    public string ReferenceNumber { get; set; } = string.Empty; // Unique external/internal txn ref

    public TransactionType TransactionType { get; set; }

    public JournalStatus Status { get; set; } = JournalStatus.Pending;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(256)]
    public string Description { get; set; } = string.Empty;

    public int? SourceAccountId { get; set; }
    public Account? SourceAccount { get; set; }

    public int? DestinationAccountId { get; set; }
    public Account? DestinationAccount { get; set; }

    [MaxLength(128)]
    public string? ExternalInstitutionName { get; set; } // For interbank transfers

    [MaxLength(64)]
    public string? ExternalRoutingOrSwift { get; set; }

    [MaxLength(64)]
    public string? ExternalAccountNumber { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public int? ReversedJournalId { get; set; } // If this is a reversal or was reversed
    public LedgerJournal? ReversedJournal { get; set; }

    // Navigation properties
    public ICollection<LedgerEntry> Entries { get; set; } = new List<LedgerEntry>();
    public FraudAlert? FraudAlert { get; set; }
}
