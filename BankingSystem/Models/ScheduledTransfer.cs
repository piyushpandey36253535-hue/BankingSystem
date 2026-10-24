using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BankingSystem.Models;

public class ScheduledTransfer
{
    [Key]
    public int Id { get; set; }

    public int SourceAccountId { get; set; }
    public Account SourceAccount { get; set; } = null!;

    public int DestinationAccountId { get; set; }
    public Account DestinationAccount { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [MaxLength(256)]
    public string Description { get; set; } = string.Empty;

    public ScheduleFrequency Frequency { get; set; } = ScheduleFrequency.Monthly;

    public DateTime NextExecutionDate { get; set; }

    public DateTime? LastExecutionDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
