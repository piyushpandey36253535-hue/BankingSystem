using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;

namespace BankingSystem.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly BankingDbContext _db;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(BankingDbContext db, ILogger<ReconciliationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReconciliationRecord> RunReconciliationAuditAsync()
    {
        var startTime = DateTime.UtcNow;

        var allEntries = await _db.LedgerEntries
            .Select(e => new { e.AccountId, e.EntryType, e.Amount })
            .ToListAsync();

        // 1. Calculate General Ledger Total Debits and Credits
        var totalDebits = allEntries
            .Where(e => e.EntryType == EntryType.Debit)
            .Sum(e => e.Amount);

        var totalCredits = allEntries
            .Where(e => e.EntryType == EntryType.Credit)
            .Sum(e => e.Amount);

        bool isLedgerBalanced = Math.Abs(totalDebits - totalCredits) < 0.0001m;

        // 2. Audit each account's cached balance against its raw ledger entries
        var accounts = await _db.Accounts.ToListAsync();
        var discrepancies = new List<object>();
        decimal totalAccountBalancesSum = 0.00m;
        decimal totalDiscrepancyAmount = 0.00m;

        foreach (var account in accounts)
        {
            totalAccountBalancesSum += account.Balance;

            var credits = allEntries
                .Where(e => e.AccountId == account.Id && e.EntryType == EntryType.Credit)
                .Sum(e => e.Amount);

            var debits = allEntries
                .Where(e => e.AccountId == account.Id && e.EntryType == EntryType.Debit)
                .Sum(e => e.Amount);

            // For Asset accounts (Vault Reserve, Clearing): Balance = Debits - Credits
            // For Liability and Equity accounts (Customer accounts, Capital Equity): Balance = Credits - Debits
            bool isAsset = account.AccountNumber.StartsWith("VAULT-") || account.AccountNumber.StartsWith("CLEARING-");
            decimal expectedBalance = isAsset
                ? (debits - credits)
                : (credits - debits);

            decimal diff = Math.Abs(account.Balance - expectedBalance);
            if (diff > 0.009m)
            {
                totalDiscrepancyAmount += diff;
                discrepancies.Add(new
                {
                    AccountId = account.Id,
                    AccountNumber = account.AccountNumber,
                    Holder = account.AccountHolderName,
                    CachedBalance = account.Balance,
                    CalculatedLedgerBalance = expectedBalance,
                    Discrepancy = diff
                });
            }
        }

        var status = (isLedgerBalanced && discrepancies.Count == 0)
            ? ReconciliationStatus.Passed
            : ReconciliationStatus.DiscrepancyDetected;

        string summary = status == ReconciliationStatus.Passed
            ? $"Reconciliation Passed: {accounts.Count} accounts verified. Ledger trial balance strictly balanced (Debits=${totalDebits:N2}, Credits=${totalCredits:N2}). Zero discrepancies."
            : $"Discrepancy Detected! {discrepancies.Count} accounts out of sync. Trial balance difference: ${Math.Abs(totalDebits - totalCredits):N2}. Total account mismatch: ${totalDiscrepancyAmount:N2}.";

        var record = new ReconciliationRecord
        {
            AuditTimestamp = startTime,
            TotalAccountsAudited = accounts.Count,
            TotalLedgerDebits = totalDebits,
            TotalLedgerCredits = totalCredits,
            IsLedgerBalanced = isLedgerBalanced,
            TotalAccountBalancesSum = totalAccountBalancesSum,
            TotalDiscrepancyAmount = totalDiscrepancyAmount,
            DiscrepancyCount = discrepancies.Count,
            Status = status,
            DiscrepancyDetailsJson = discrepancies.Count > 0 ? JsonSerializer.Serialize(discrepancies) : null,
            SummaryMessage = summary
        };

        _db.ReconciliationRecords.Add(record);
        await _db.SaveChangesAsync();

        if (status == ReconciliationStatus.Passed)
        {
            _logger.LogInformation("Reconciliation Audit #{Id} PASSED. Audited {Count} accounts. Debits=${Debits:N2}, Credits=${Credits:N2}",
                record.Id, accounts.Count, totalDebits, totalCredits);
        }
        else
        {
            _logger.LogWarning("Reconciliation Audit #{Id} FAILED with {Count} discrepancies! Summary: {Summary}",
                record.Id, discrepancies.Count, summary);
        }

        return record;
    }

    public async Task<List<ReconciliationRecord>> GetReconciliationHistoryAsync(int count = 20)
    {
        return await _db.ReconciliationRecords
            .AsNoTracking()
            .OrderByDescending(r => r.AuditTimestamp)
            .Take(count)
            .ToListAsync();
    }

    public async Task<ReconciliationRecord?> GetLatestReconciliationAsync()
    {
        return await _db.ReconciliationRecords
            .AsNoTracking()
            .OrderByDescending(r => r.AuditTimestamp)
            .FirstOrDefaultAsync();
    }
}
