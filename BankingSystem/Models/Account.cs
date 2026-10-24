using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Models;

public class Account
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(32)]
    public string AccountNumber { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string RoutingNumber { get; set; } = "021000021"; // Standard simulated ABA Fedwire routing

    [Required]
    [MaxLength(128)]
    public string AccountHolderName { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Phone { get; set; } = string.Empty;

    public AccountType AccountType { get; set; } = AccountType.Checking;

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public KycStatus KycStatus { get; set; } = KycStatus.Unverified;

    [MaxLength(64)]
    public string? KycDocumentType { get; set; } // Passport, Driver License, National ID

    [MaxLength(64)]
    public string? KycDocumentNumber { get; set; } // Masked document ID

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } = 0.00m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal AvailableBalance { get; set; } = 0.00m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal DailyTransferLimit { get; set; } = 1000.00m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal SingleTransferLimit { get; set; } = 500.00m;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
    public ICollection<FraudAlert> FraudAlerts { get; set; } = new List<FraudAlert>();
}
