using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Models;

public class LedgerEntry
{
    [Key]
    public long Id { get; set; }

    public int LedgerJournalId { get; set; }
    public LedgerJournal Journal { get; set; } = null!;

    public int AccountId { get; set; }
    public Account Account { get; set; } = null!;

    public EntryType EntryType { get; set; } // Debit or Credit

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RunningBalanceAfter { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? Note { get; set; }
}
