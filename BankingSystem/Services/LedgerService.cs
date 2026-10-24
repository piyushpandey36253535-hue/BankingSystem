using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;

namespace BankingSystem.Services;

public class LedgerService : ILedgerService
{
    private readonly BankingDbContext _db;
    private readonly ILogger<LedgerService> _logger;

    public LedgerService(BankingDbContext db, ILogger<LedgerService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LedgerJournal> PostJournalAsync(
        TransactionType transactionType,
        decimal amount,
        string description,
        string referenceNumber,
        int? sourceAccountId,
        int? destinationAccountId,
        string? externalInstitution = null,
        string? externalRouting = null,
        string? externalAccount = null,
        bool isDeposit = false)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Transaction amount must be strictly greater than zero.", nameof(amount));
        }

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var journalNumber = $"JRN-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

            var journal = new LedgerJournal
            {
                JournalNumber = journalNumber,
                ReferenceNumber = referenceNumber,
                TransactionType = transactionType,
                Status = JournalStatus.Posted,
                Amount = amount,
                Description = description,
                SourceAccountId = sourceAccountId,
                DestinationAccountId = destinationAccountId,
                ExternalInstitutionName = externalInstitution,
                ExternalRoutingOrSwift = externalRouting,
                ExternalAccountNumber = externalAccount,
                Timestamp = DateTime.UtcNow
            };

            _db.LedgerJournals.Add(journal);
            await _db.SaveChangesAsync();

            // Find System Vault and Clearing Accounts
            var vaultAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == "VAULT-RESERVE-01")
                ?? throw new InvalidOperationException("System Central Vault account (VAULT-RESERVE-01) not found.");
            
            var clearingAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == "CLEARING-ACH-01")
                ?? vaultAccount;

            var entries = new List<LedgerEntry>();

            if (transactionType == TransactionType.Deposit || isDeposit)
            {
                // Customer Deposit:
                // Debit Vault Reserve (+Asset)
                // Credit Customer Account (+Liability)
                if (!destinationAccountId.HasValue)
                {
                    throw new InvalidOperationException("Destination account is required for deposits.");
                }

                var customer = await _db.Accounts.FindAsync(destinationAccountId.Value)
                    ?? throw new KeyNotFoundException($"Destination account ID {destinationAccountId} not found.");

                vaultAccount.Balance += amount;
                vaultAccount.AvailableBalance += amount;
                vaultAccount.UpdatedAt = DateTime.UtcNow;

                customer.Balance += amount;
                customer.AvailableBalance += amount;
                customer.UpdatedAt = DateTime.UtcNow;

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = vaultAccount.Id,
                    EntryType = EntryType.Debit,
                    Amount = amount,
                    RunningBalanceAfter = vaultAccount.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Cash deposit reserve inflow for account {customer.AccountNumber}"
                });

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = customer.Id,
                    EntryType = EntryType.Credit,
                    Amount = amount,
                    RunningBalanceAfter = customer.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Customer deposit credited from Central Vault"
                });
            }
            else if (transactionType == TransactionType.Withdrawal)
            {
                // Customer Withdrawal:
                // Debit Customer Account (-Liability)
                // Credit Vault Reserve (-Asset)
                if (!sourceAccountId.HasValue)
                {
                    throw new InvalidOperationException("Source account is required for withdrawals.");
                }

                var customer = await _db.Accounts.FindAsync(sourceAccountId.Value)
                    ?? throw new KeyNotFoundException($"Source account ID {sourceAccountId} not found.");

                if (customer.AvailableBalance < amount)
                {
                    throw new InvalidOperationException($"Insufficient funds. Available balance: ${customer.AvailableBalance:N2}");
                }

                customer.Balance -= amount;
                customer.AvailableBalance -= amount;
                customer.UpdatedAt = DateTime.UtcNow;

                vaultAccount.Balance -= amount;
                vaultAccount.AvailableBalance -= amount;
                vaultAccount.UpdatedAt = DateTime.UtcNow;

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = customer.Id,
                    EntryType = EntryType.Debit,
                    Amount = amount,
                    RunningBalanceAfter = customer.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = "Customer cash withdrawal debit"
                });

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = vaultAccount.Id,
                    EntryType = EntryType.Credit,
                    Amount = amount,
                    RunningBalanceAfter = vaultAccount.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Vault reserve outflow to customer {customer.AccountNumber}"
                });
            }
            else if (transactionType == TransactionType.IntraBankTransfer)
            {
                // Intra-bank Transfer:
                // Debit Source Account (-Liability)
                // Credit Destination Account (+Liability)
                if (!sourceAccountId.HasValue || !destinationAccountId.HasValue)
                {
                    throw new InvalidOperationException("Both source and destination accounts are required for intra-bank transfer.");
                }

                var source = await _db.Accounts.FindAsync(sourceAccountId.Value)
                    ?? throw new KeyNotFoundException($"Source account ID {sourceAccountId} not found.");
                var dest = await _db.Accounts.FindAsync(destinationAccountId.Value)
                    ?? throw new KeyNotFoundException($"Destination account ID {destinationAccountId} not found.");

                if (source.AvailableBalance < amount)
                {
                    throw new InvalidOperationException($"Insufficient funds in source account. Available: ${source.AvailableBalance:N2}");
                }

                source.Balance -= amount;
                source.AvailableBalance -= amount;
                source.UpdatedAt = DateTime.UtcNow;

                dest.Balance += amount;
                dest.AvailableBalance += amount;
                dest.UpdatedAt = DateTime.UtcNow;

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = source.Id,
                    EntryType = EntryType.Debit,
                    Amount = amount,
                    RunningBalanceAfter = source.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Transfer outflow to {dest.AccountNumber} ({dest.AccountHolderName})"
                });

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = dest.Id,
                    EntryType = EntryType.Credit,
                    Amount = amount,
                    RunningBalanceAfter = dest.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Transfer inflow from {source.AccountNumber} ({source.AccountHolderName})"
                });
            }
            else if (transactionType == TransactionType.InterBankTransfer)
            {
                // Outgoing Inter-bank Transfer (Fedwire / ACH):
                // Debit Customer Source Account (-Liability)
                // Credit Interbank Clearing Account (-Clearing Reserve)
                if (!sourceAccountId.HasValue)
                {
                    throw new InvalidOperationException("Source account is required for inter-bank transfer.");
                }

                var source = await _db.Accounts.FindAsync(sourceAccountId.Value)
                    ?? throw new KeyNotFoundException($"Source account ID {sourceAccountId} not found.");

                if (source.AvailableBalance < amount)
                {
                    throw new InvalidOperationException($"Insufficient funds in source account. Available: ${source.AvailableBalance:N2}");
                }

                source.Balance -= amount;
                source.AvailableBalance -= amount;
                source.UpdatedAt = DateTime.UtcNow;

                clearingAccount.Balance -= amount;
                clearingAccount.AvailableBalance -= amount;
                clearingAccount.UpdatedAt = DateTime.UtcNow;

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = source.Id,
                    EntryType = EntryType.Debit,
                    Amount = amount,
                    RunningBalanceAfter = source.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Inter-bank ACH/Fedwire debit to {externalInstitution ?? "External Bank"} ({externalAccount ?? "N/A"})"
                });

                entries.Add(new LedgerEntry
                {
                    LedgerJournalId = journal.Id,
                    AccountId = clearingAccount.Id,
                    EntryType = EntryType.Credit,
                    Amount = amount,
                    RunningBalanceAfter = clearingAccount.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Clearing settlement credit for {source.AccountNumber}"
                });
            }

            // Invariant Check: Sum of Debits MUST strictly equal Sum of Credits!
            var totalDebits = entries.Where(e => e.EntryType == EntryType.Debit).Sum(e => e.Amount);
            var totalCredits = entries.Where(e => e.EntryType == EntryType.Credit).Sum(e => e.Amount);

            if (totalDebits != totalCredits)
            {
                throw new InvalidOperationException($"Double-entry balance mismatch! Debits (${totalDebits:N2}) != Credits (${totalCredits:N2})");
            }

            _db.LedgerEntries.AddRange(entries);
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Posted balanced double-entry Journal {JournalNumber}: Amount=${Amount:N2}, Debits=${Debits:N2}, Credits=${Credits:N2}",
                journal.JournalNumber, amount, totalDebits, totalCredits);

            return journal;
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Failed to post ledger journal: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<LedgerJournal> ReverseJournalAsync(int journalId, string reason, string reversedBy)
    {
        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var original = await _db.LedgerJournals
                .Include(j => j.Entries)
                .FirstOrDefaultAsync(j => j.Id == journalId)
                ?? throw new KeyNotFoundException($"Journal ID {journalId} not found.");

            if (original.Status == JournalStatus.Reversed)
            {
                throw new InvalidOperationException("This journal has already been reversed.");
            }

            if (original.Status != JournalStatus.Posted)
            {
                throw new InvalidOperationException($"Only Posted journals can be reversed. Current status: {original.Status}");
            }

            var reversalJournalNumber = $"REV-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
            var reversalJournal = new LedgerJournal
            {
                JournalNumber = reversalJournalNumber,
                ReferenceNumber = $"REV-REF-{original.ReferenceNumber}",
                TransactionType = TransactionType.Reversal,
                Status = JournalStatus.Posted,
                Amount = original.Amount,
                Description = $"REVERSAL: {reason} (Ref original: {original.JournalNumber}) by {reversedBy}",
                SourceAccountId = original.DestinationAccountId, // Invert source & dest for reference
                DestinationAccountId = original.SourceAccountId,
                ReversedJournalId = original.Id,
                Timestamp = DateTime.UtcNow
            };

            _db.LedgerJournals.Add(reversalJournal);
            await _db.SaveChangesAsync();

            var reversalEntries = new List<LedgerEntry>();

            // For every entry in original, post the opposite
            foreach (var origEntry in original.Entries)
            {
                var account = await _db.Accounts.FindAsync(origEntry.AccountId)
                    ?? throw new KeyNotFoundException($"Account ID {origEntry.AccountId} not found.");

                var oppositeType = origEntry.EntryType == EntryType.Debit ? EntryType.Credit : EntryType.Debit;

                if (oppositeType == EntryType.Debit)
                {
                    account.Balance -= origEntry.Amount;
                    account.AvailableBalance -= origEntry.Amount;
                }
                else
                {
                    account.Balance += origEntry.Amount;
                    account.AvailableBalance += origEntry.Amount;
                }
                account.UpdatedAt = DateTime.UtcNow;

                reversalEntries.Add(new LedgerEntry
                {
                    LedgerJournalId = reversalJournal.Id,
                    AccountId = account.Id,
                    EntryType = oppositeType,
                    Amount = origEntry.Amount,
                    RunningBalanceAfter = account.Balance,
                    Timestamp = DateTime.UtcNow,
                    Note = $"Opposite entry for reversal of Journal {original.JournalNumber}"
                });
            }

            // Invariant verification
            var totalDebits = reversalEntries.Where(e => e.EntryType == EntryType.Debit).Sum(e => e.Amount);
            var totalCredits = reversalEntries.Where(e => e.EntryType == EntryType.Credit).Sum(e => e.Amount);
            if (totalDebits != totalCredits)
            {
                throw new InvalidOperationException("Reversal entries are not balanced!");
            }

            original.Status = JournalStatus.Reversed;
            _db.LedgerEntries.AddRange(reversalEntries);
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Successfully reversed Journal {OriginalJournal} via Reversal Journal {ReversalJournal}",
                original.JournalNumber, reversalJournal.JournalNumber);

            return reversalJournal;
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Failed to reverse journal ID {JournalId}", journalId);
            throw;
        }
    }

    public async Task<List<LedgerJournal>> GetJournalsAsync(int page = 1, int pageSize = 50, int? accountId = null)
    {
        var query = _db.LedgerJournals
            .Include(j => j.SourceAccount)
            .Include(j => j.DestinationAccount)
            .Include(j => j.Entries)
                .ThenInclude(e => e.Account)
            .Include(j => j.FraudAlert)
            .AsNoTracking();

        if (accountId.HasValue)
        {
            query = query.Where(j => j.SourceAccountId == accountId || j.DestinationAccountId == accountId || j.Entries.Any(e => e.AccountId == accountId));
        }

        return await query
            .OrderByDescending(j => j.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<LedgerJournal?> GetJournalByIdAsync(int journalId)
    {
        return await _db.LedgerJournals
            .Include(j => j.SourceAccount)
            .Include(j => j.DestinationAccount)
            .Include(j => j.Entries)
                .ThenInclude(e => e.Account)
            .Include(j => j.FraudAlert)
            .FirstOrDefaultAsync(j => j.Id == journalId);
    }

    public async Task<(decimal totalDebits, decimal totalCredits, bool isBalanced)> VerifyTrialBalanceAsync()
    {
        var debitAmounts = await _db.LedgerEntries
            .Where(e => e.EntryType == EntryType.Debit)
            .Select(e => e.Amount)
            .ToListAsync();

        var creditAmounts = await _db.LedgerEntries
            .Where(e => e.EntryType == EntryType.Credit)
            .Select(e => e.Amount)
            .ToListAsync();

        var totalDebits = debitAmounts.Sum();
        var totalCredits = creditAmounts.Sum();

        var isBalanced = Math.Abs(totalDebits - totalCredits) < 0.0001m;
        return (totalDebits, totalCredits, isBalanced);
    }
}
