using Microsoft.EntityFrameworkCore;
using BankingSystem.Models;

namespace BankingSystem.Data;

public class BankingDbContext : DbContext
{
    public BankingDbContext(DbContextOptions<BankingDbContext> options) : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<LedgerJournal> LedgerJournals => Set<LedgerJournal>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<FraudAlert> FraudAlerts => Set<FraudAlert>();
    public DbSet<ScheduledTransfer> ScheduledTransfers => Set<ScheduledTransfer>();
    public DbSet<ReconciliationRecord> ReconciliationRecords => Set<ReconciliationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Account configurations
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasIndex(a => a.AccountNumber).IsUnique();
            entity.HasIndex(a => a.Email);

            entity.Property(a => a.Balance).HasPrecision(18, 2);
            entity.Property(a => a.AvailableBalance).HasPrecision(18, 2);
            entity.Property(a => a.DailyTransferLimit).HasPrecision(18, 2);
            entity.Property(a => a.SingleTransferLimit).HasPrecision(18, 2);
        });

        // LedgerJournal configurations
        modelBuilder.Entity<LedgerJournal>(entity =>
        {
            entity.HasIndex(j => j.JournalNumber).IsUnique();
            entity.HasIndex(j => j.ReferenceNumber).IsUnique();
            entity.HasIndex(j => j.Timestamp);

            entity.Property(j => j.Amount).HasPrecision(18, 2);

            entity.HasOne(j => j.SourceAccount)
                  .WithMany()
                  .HasForeignKey(j => j.SourceAccountId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(j => j.DestinationAccount)
                  .WithMany()
                  .HasForeignKey(j => j.DestinationAccountId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(j => j.ReversedJournal)
                  .WithMany()
                  .HasForeignKey(j => j.ReversedJournalId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // LedgerEntry configurations
        modelBuilder.Entity<LedgerEntry>(entity =>
        {
            entity.HasIndex(e => e.Timestamp);
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.RunningBalanceAfter).HasPrecision(18, 2);

            entity.HasOne(e => e.Journal)
                  .WithMany(j => j.Entries)
                  .HasForeignKey(e => e.LedgerJournalId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Account)
                  .WithMany(a => a.LedgerEntries)
                  .HasForeignKey(e => e.AccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // FraudAlert configurations
        modelBuilder.Entity<FraudAlert>(entity =>
        {
            entity.HasIndex(f => f.CreatedAt);
            entity.HasIndex(f => f.Status);
            entity.Property(f => f.FlaggedAmount).HasPrecision(18, 2);

            entity.HasOne(f => f.Account)
                  .WithMany(a => a.FraudAlerts)
                  .HasForeignKey(f => f.AccountId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Journal)
                  .WithOne(j => j.FraudAlert)
                  .HasForeignKey<FraudAlert>(f => f.LedgerJournalId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ScheduledTransfer configurations
        modelBuilder.Entity<ScheduledTransfer>(entity =>
        {
            entity.Property(s => s.Amount).HasPrecision(18, 2);

            entity.HasOne(s => s.SourceAccount)
                  .WithMany()
                  .HasForeignKey(s => s.SourceAccountId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(s => s.DestinationAccount)
                  .WithMany()
                  .HasForeignKey(s => s.DestinationAccountId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ReconciliationRecord configurations
        modelBuilder.Entity<ReconciliationRecord>(entity =>
        {
            entity.Property(r => r.TotalLedgerDebits).HasPrecision(18, 2);
            entity.Property(r => r.TotalLedgerCredits).HasPrecision(18, 2);
            entity.Property(r => r.TotalAccountBalancesSum).HasPrecision(18, 2);
            entity.Property(r => r.TotalDiscrepancyAmount).HasPrecision(18, 2);
        });
    }
}
