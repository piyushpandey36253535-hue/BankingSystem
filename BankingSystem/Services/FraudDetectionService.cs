using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;

namespace BankingSystem.Services;

public class FraudDetectionService : IFraudDetectionService
{
    private readonly BankingDbContext _db;
    private readonly ILogger<FraudDetectionService> _logger;

    public FraudDetectionService(BankingDbContext db, ILogger<FraudDetectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<FraudEvaluationResult> EvaluateTransferAsync(
        Account sourceAccount,
        decimal amount,
        string? externalInstitution,
        bool isInterbank)
    {
        var rules = new List<string>();
        var details = new List<string>();
        int riskScore = 0;

        // Rule 1: Account Status check
        if (sourceAccount.Status != AccountStatus.Active)
        {
            rules.Add("ACCOUNT_NOT_ACTIVE");
            details.Add($"Account status is {sourceAccount.Status}. All outgoing operations blocked.");
            return new FraudEvaluationResult(
                IsAllowed: false,
                RequiresReview: true,
                RiskScore: 100,
                Severity: AlertSeverity.Critical,
                TriggeredRules: rules,
                Details: string.Join("; ", details)
            );
        }

        // Rule 2: Single Transaction Limit
        if (amount > sourceAccount.SingleTransferLimit)
        {
            rules.Add("SINGLE_LIMIT_EXCEEDED");
            details.Add($"Amount (${amount:N2}) exceeds single transfer limit (${sourceAccount.SingleTransferLimit:N2})");
            riskScore += 40;
        }

        // Rule 3: Daily Limit Rolling Check (Past 24 hours)
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);
        var rolling24hEntries = await _db.LedgerEntries
            .Where(e => e.AccountId == sourceAccount.Id && e.EntryType == EntryType.Debit && e.Timestamp >= oneDayAgo)
            .Select(e => e.Amount)
            .ToListAsync();
        var rolling24hOutflow = rolling24hEntries.Sum();

        if (rolling24hOutflow + amount > sourceAccount.DailyTransferLimit)
        {
            rules.Add("DAILY_LIMIT_EXCEEDED");
            details.Add($"24-hour cumulative outflow (${rolling24hOutflow + amount:N2}) breaches daily limit (${sourceAccount.DailyTransferLimit:N2})");
            riskScore += 45;
        }

        // Rule 4: Velocity Check (transfers in last 60 seconds)
        var oneMinuteAgo = DateTime.UtcNow.AddMinutes(-1);
        var recentTransfersCount = await _db.LedgerEntries
            .Where(e => e.AccountId == sourceAccount.Id && e.EntryType == EntryType.Debit && e.Timestamp >= oneMinuteAgo)
            .CountAsync();

        if (recentTransfersCount >= 2)
        {
            rules.Add("HIGH_VELOCITY_BURST");
            details.Add($"High frequency activity: {recentTransfersCount} transfers executed within the last 60 seconds");
            riskScore += 35;
        }

        // Rule 5: AML Structuring / Smurfing Detection ($9,000 to $9,999.99)
        // Fraudsters frequently break down larger transfers to evade the $10,000 BSA/FinCEN threshold
        if (amount >= 9000.00m && amount < 10000.00m)
        {
            rules.Add("STRUCTURING_AML_SUSPECT");
            details.Add($"Transaction amount ${amount:N2} falls within high-risk AML structuring threshold ($9,000 - $9,999)");
            riskScore += 65;
        }

        // Rule 6: Historical Amount Spike (> 4x account average)
        var pastEntries = await _db.LedgerEntries
            .Where(e => e.AccountId == sourceAccount.Id && e.EntryType == EntryType.Debit)
            .OrderByDescending(e => e.Timestamp)
            .Take(10)
            .Select(e => e.Amount)
            .ToListAsync();

        if (pastEntries.Count >= 3)
        {
            var averageAmount = pastEntries.Average();
            if (averageAmount > 0 && amount > (averageAmount * 4.0m) && amount > 2000.00m)
            {
                rules.Add("ANOMALOUS_AMOUNT_SPIKE");
                details.Add($"Transfer amount (${amount:N2}) is {amount / averageAmount:N1}x greater than customer's 10-tx average (${averageAmount:N2})");
                riskScore += 30;
            }
        }

        // Rule 7: KYC Verification Tier Check
        if (sourceAccount.KycStatus != KycStatus.Verified && amount > 1000.00m)
        {
            rules.Add("UNVERIFIED_HIGH_VALUE");
            details.Add($"Account is not KYC verified ({sourceAccount.KycStatus}), yet attempting high-value transfer of ${amount:N2}");
            riskScore += 25;
        }

        // Rule 8: Rapid Drain Check (deposit within last 15 minutes, followed by > 85% drain)
        var fifteenMinutesAgo = DateTime.UtcNow.AddMinutes(-15);
        var recentDeposit = await _db.LedgerEntries
            .Where(e => e.AccountId == sourceAccount.Id && e.EntryType == EntryType.Credit && e.Timestamp >= fifteenMinutesAgo)
            .OrderByDescending(e => e.Timestamp)
            .FirstOrDefaultAsync();

        if (recentDeposit != null && sourceAccount.Balance > 0)
        {
            var drainRatio = amount / sourceAccount.Balance;
            if (drainRatio >= 0.85m && amount > 1000.00m)
            {
                rules.Add("RAPID_DRAIN_AFTER_DEPOSIT");
                details.Add($"Suspicious fast drain: Attempting to withdraw {drainRatio:P0} of balance shortly after receiving deposit");
                riskScore += 35;
            }
        }

        // Determine severity & allowance
        riskScore = Math.Min(100, riskScore);

        AlertSeverity severity = riskScore switch
        {
            >= 80 => AlertSeverity.Critical,
            >= 60 => AlertSeverity.High,
            >= 35 => AlertSeverity.Medium,
            _ => AlertSeverity.Low
        };

        // If score >= 60, hold the transaction for manual compliance review
        bool isAllowed = riskScore < 60;
        bool requiresReview = riskScore >= 35;

        return new FraudEvaluationResult(
            IsAllowed: isAllowed,
            RequiresReview: requiresReview,
            RiskScore: riskScore,
            Severity: severity,
            TriggeredRules: rules,
            Details: details.Count > 0 ? string.Join("; ", details) : "Transaction passed all standard fraud and velocity checks."
        );
    }

    public async Task<FraudAlert> CreateAlertAsync(
        int accountId,
        int? journalId,
        decimal flaggedAmount,
        FraudEvaluationResult evaluation)
    {
        var alert = new FraudAlert
        {
            AccountId = accountId,
            LedgerJournalId = journalId,
            FlaggedAmount = flaggedAmount,
            RiskScore = evaluation.RiskScore,
            Severity = evaluation.Severity,
            Status = evaluation.IsAllowed ? FraudAlertStatus.Open : FraudAlertStatus.UnderReview,
            TriggeredRules = string.Join(", ", evaluation.TriggeredRules),
            Details = evaluation.Details,
            CreatedAt = DateTime.UtcNow
        };

        _db.FraudAlerts.Add(alert);
        await _db.SaveChangesAsync();

        _logger.LogWarning("Fraud Alert Created: Alert #{Id}, Account #{AccountId}, Score={Score}, Rules={Rules}",
            alert.Id, accountId, alert.RiskScore, alert.TriggeredRules);

        return alert;
    }

    public async Task<FraudAlert> ReviewAlertAsync(int alertId, ReviewFraudAlertRequest request)
    {
        var alert = await _db.FraudAlerts
            .Include(a => a.Journal)
            .Include(a => a.Account)
            .FirstOrDefaultAsync(a => a.Id == alertId)
            ?? throw new KeyNotFoundException($"Fraud Alert ID {alertId} not found.");

        if (alert.Status == FraudAlertStatus.Approved || alert.Status == FraudAlertStatus.Rejected)
        {
            throw new InvalidOperationException($"Alert #{alertId} has already been resolved as {alert.Status}.");
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            alert.Status = request.Decision;
            alert.ReviewNotes = request.ReviewNotes;
            alert.ReviewedBy = request.ReviewedBy;
            alert.ResolvedAt = DateTime.UtcNow;

            // If the transaction was held in LedgerJournals
            if (alert.LedgerJournalId.HasValue && alert.Journal != null)
            {
                var journal = alert.Journal;
                var source = await _db.Accounts.FindAsync(journal.SourceAccountId);

                if (request.Decision == FraudAlertStatus.Approved)
                {
                    // Approved: Release the hold and post the double-entry entries
                    journal.Status = JournalStatus.Posted;

                    if (journal.TransactionType == TransactionType.IntraBankTransfer && journal.DestinationAccountId.HasValue)
                    {
                        var dest = await _db.Accounts.FindAsync(journal.DestinationAccountId.Value)
                            ?? throw new InvalidOperationException("Destination account not found.");

                        // Deduct balance from source (available was already deducted during hold)
                        if (source != null)
                        {
                            source.Balance -= journal.Amount;
                            source.UpdatedAt = DateTime.UtcNow;

                            _db.LedgerEntries.Add(new LedgerEntry
                            {
                                LedgerJournalId = journal.Id,
                                AccountId = source.Id,
                                EntryType = EntryType.Debit,
                                Amount = journal.Amount,
                                RunningBalanceAfter = source.Balance,
                                Timestamp = DateTime.UtcNow,
                                Note = $"Compliance approved release: Transfer to {dest.AccountNumber}"
                            });
                        }

                        dest.Balance += journal.Amount;
                        dest.AvailableBalance += journal.Amount;
                        dest.UpdatedAt = DateTime.UtcNow;

                        _db.LedgerEntries.Add(new LedgerEntry
                        {
                            LedgerJournalId = journal.Id,
                            AccountId = dest.Id,
                            EntryType = EntryType.Credit,
                            Amount = journal.Amount,
                            RunningBalanceAfter = dest.Balance,
                            Timestamp = DateTime.UtcNow,
                            Note = $"Compliance approved credit from {source?.AccountNumber}"
                        });
                    }
                    else if (journal.TransactionType == TransactionType.InterBankTransfer)
                    {
                        var clearing = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == "CLEARING-ACH-01")
                            ?? await _db.Accounts.FirstAsync(a => a.AccountNumber == "VAULT-RESERVE-01");

                        if (source != null)
                        {
                            source.Balance -= journal.Amount;
                            source.UpdatedAt = DateTime.UtcNow;

                            _db.LedgerEntries.Add(new LedgerEntry
                            {
                                LedgerJournalId = journal.Id,
                                AccountId = source.Id,
                                EntryType = EntryType.Debit,
                                Amount = journal.Amount,
                                RunningBalanceAfter = source.Balance,
                                Timestamp = DateTime.UtcNow,
                                Note = $"Compliance approved ACH release to {journal.ExternalInstitutionName}"
                            });
                        }

                        clearing.Balance -= journal.Amount;
                        clearing.AvailableBalance -= journal.Amount;
                        clearing.UpdatedAt = DateTime.UtcNow;

                        _db.LedgerEntries.Add(new LedgerEntry
                        {
                            LedgerJournalId = journal.Id,
                            AccountId = clearing.Id,
                            EntryType = EntryType.Credit,
                            Amount = journal.Amount,
                            RunningBalanceAfter = clearing.Balance,
                            Timestamp = DateTime.UtcNow,
                            Note = $"Compliance approved clearing settlement"
                        });
                    }
                }
                else if (request.Decision == FraudAlertStatus.Rejected)
                {
                    // Rejected: Cancel held journal and restore source AvailableBalance
                    journal.Status = JournalStatus.Rejected;
                    if (source != null)
                    {
                        source.AvailableBalance += journal.Amount; // Release hold
                        source.UpdatedAt = DateTime.UtcNow;
                    }
                }
            }

            // Optionally freeze account if fraud was confirmed
            if (request.FreezeAccountIfRejected && request.Decision == FraudAlertStatus.Rejected)
            {
                alert.Account.Status = AccountStatus.Frozen;
                alert.Account.UpdatedAt = DateTime.UtcNow;
                _logger.LogWarning("Account {AccountNumber} was FROZEN by compliance officer {User} following rejected alert #{AlertId}",
                    alert.Account.AccountNumber, request.ReviewedBy, alert.Id);
            }

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Fraud Alert #{Id} reviewed and set to {Decision} by {User}",
                alert.Id, alert.Status, request.ReviewedBy);

            return alert;
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Failed to review fraud alert #{Id}: {Message}", alertId, ex.Message);
            throw;
        }
    }

    public async Task<List<FraudAlert>> GetAlertsAsync(string? status = null, int? accountId = null)
    {
        var query = _db.FraudAlerts
            .Include(a => a.Account)
            .Include(a => a.Journal)
            .AsNoTracking();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<FraudAlertStatus>(status, true, out var alertStatus))
        {
            query = query.Where(a => a.Status == alertStatus);
        }

        if (accountId.HasValue)
        {
            query = query.Where(a => a.AccountId == accountId);
        }

        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    public async Task<FraudAlert?> GetAlertByIdAsync(int alertId)
    {
        return await _db.FraudAlerts
            .Include(a => a.Account)
            .Include(a => a.Journal)
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == alertId);
    }

    public async Task<List<FraudAlert>> SimulateAttackScenarioAsync(int accountId, string scenarioType)
    {
        var account = await _db.Accounts.FindAsync(accountId)
            ?? throw new KeyNotFoundException($"Account ID {accountId} not found.");

        var generatedAlerts = new List<FraudAlert>();

        if (scenarioType.Equals("Structuring", StringComparison.OrdinalIgnoreCase))
        {
            // Simulate 2 successive transfers of $9,800 to avoid $10k reporting
            var eval = new FraudEvaluationResult(
                IsAllowed: false,
                RequiresReview: true,
                RiskScore: 85,
                Severity: AlertSeverity.High,
                TriggeredRules: new List<string> { "STRUCTURING_AML_SUSPECT", "ANOMALOUS_AMOUNT_SPIKE" },
                Details: "Synthetic attack test: Smurfing transfer of $9,850.00 flagged for evasion of CTR threshold."
            );
            var alert = await CreateAlertAsync(account.Id, null, 9850.00m, eval);
            generatedAlerts.Add(alert);
        }
        else if (scenarioType.Equals("VelocityDrain", StringComparison.OrdinalIgnoreCase))
        {
            // Simulate automated bot rapid burst
            var eval = new FraudEvaluationResult(
                IsAllowed: false,
                RequiresReview: true,
                RiskScore: 90,
                Severity: AlertSeverity.Critical,
                TriggeredRules: new List<string> { "HIGH_VELOCITY_BURST", "RAPID_DRAIN_AFTER_DEPOSIT" },
                Details: "Synthetic attack test: 4 rapid automated drain transfers executed in sub-minute interval."
            );
            var alert = await CreateAlertAsync(account.Id, null, 4500.00m, eval);
            generatedAlerts.Add(alert);
        }
        else
        {
            // Limit breach
            var eval = new FraudEvaluationResult(
                IsAllowed: false,
                RequiresReview: true,
                RiskScore: 75,
                Severity: AlertSeverity.High,
                TriggeredRules: new List<string> { "SINGLE_LIMIT_EXCEEDED", "DAILY_LIMIT_EXCEEDED" },
                Details: $"Synthetic attack test: Transfer of $75,000.00 exceeds configured single limit (${account.SingleTransferLimit:N2})."
            );
            var alert = await CreateAlertAsync(account.Id, null, 75000.00m, eval);
            generatedAlerts.Add(alert);
        }

        return generatedAlerts;
    }
}
