using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Models;

public class FraudAlert
{
    [Key]
    public int Id { get; set; }

    public int? LedgerJournalId { get; set; }
    public LedgerJournal? Journal { get; set; }

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public int RiskScore { get; set; } // 0 - 100

    public AlertSeverity Severity { get; set; } = AlertSeverity.Medium;

    public FraudAlertStatus Status { get; set; } = FraudAlertStatus.Open;

    [Required]
    [MaxLength(256)]
    public string TriggeredRules { get; set; } = string.Empty; // Comma-separated rule codes

    [Required]
    [MaxLength(512)]
    public string Details { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal FlaggedAmount { get; set; }

    [MaxLength(512)]
    public string? ReviewNotes { get; set; }

    [MaxLength(64)]
    public string? ReviewedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ResolvedAt { get; set; }
}
