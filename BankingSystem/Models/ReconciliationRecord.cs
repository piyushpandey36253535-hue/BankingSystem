using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Models;

public class ReconciliationRecord
{
    [Key]
    public int Id { get; set; }

    public DateTime AuditTimestamp { get; set; } = DateTime.UtcNow;

    public int TotalAccountsAudited { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalLedgerDebits { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalLedgerCredits { get; set; }

    public bool IsLedgerBalanced { get; set; } = true; // Total Debits == Total Credits

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAccountBalancesSum { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDiscrepancyAmount { get; set; }

    public int DiscrepancyCount { get; set; }

    public ReconciliationStatus Status { get; set; } = ReconciliationStatus.Passed;

    [MaxLength(2048)]
    public string? DiscrepancyDetailsJson { get; set; }

    [MaxLength(256)]
    public string SummaryMessage { get; set; } = string.Empty;
}
