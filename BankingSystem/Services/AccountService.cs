using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;

namespace BankingSystem.Services;

public class AccountService : IAccountService
{
    private readonly BankingDbContext _db;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<AccountService> _logger;

    public AccountService(BankingDbContext db, ILedgerService ledgerService, ILogger<AccountService> logger)
    {
        _db = db;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<Account> CreateAccountAsync(CreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AccountHolderName))
        {
            throw new ArgumentException("Account holder name is required.", nameof(request.AccountHolderName));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required.", nameof(request.Email));
        }

        // Generate a realistic, unique 10-digit account number with prefix
        string accountNumber;
        var random = new Random();
        do
        {
            var num = random.NextInt64(1000000000, 9999999999);
            accountNumber = $"ACCT-{num}";
        } while (await _db.Accounts.AnyAsync(a => a.AccountNumber == accountNumber));

        // Default limits by account type
        decimal dailyLimit = request.AccountType switch
        {
            AccountType.Business => 100000.00m,
            AccountType.Savings => 5000.00m,
            _ => 2500.00m // Checking
        };

        decimal singleLimit = request.AccountType switch
        {
            AccountType.Business => 50000.00m,
            AccountType.Savings => 2500.00m,
            _ => 1000.00m
        };

        var account = new Account
        {
            AccountNumber = accountNumber,
            RoutingNumber = "021000021", // Federal Reserve New York ABA routing representation
            AccountHolderName = request.AccountHolderName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = request.Phone?.Trim() ?? string.Empty,
            AccountType = request.AccountType,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Unverified,
            Balance = 0.00m,
            AvailableBalance = 0.00m,
            DailyTransferLimit = dailyLimit,
            SingleTransferLimit = singleLimit,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Accounts.Add(account);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created new account: {AccountNumber} for {Holder}", account.AccountNumber, account.AccountHolderName);

        // If there is an initial deposit requested, fund via double-entry from Central Vault
        if (request.InitialDeposit > 0)
        {
            var refNumber = $"DEP-INIT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            await _ledgerService.PostJournalAsync(
                transactionType: TransactionType.Deposit,
                amount: request.InitialDeposit,
                description: $"Opening Balance Deposit - {account.AccountHolderName}",
                referenceNumber: refNumber,
                sourceAccountId: null,
                destinationAccountId: account.Id,
                isDeposit: true
            );

            // Reload account with updated balance
            await _db.Entry(account).ReloadAsync();
        }

        return account;
    }

    public async Task<Account> VerifyKycAsync(int accountId, VerifyKycRequest request)
    {
        var account = await _db.Accounts.FindAsync(accountId)
            ?? throw new KeyNotFoundException($"Account ID {accountId} not found.");

        if (string.IsNullOrWhiteSpace(request.DocumentType))
        {
            throw new ArgumentException("Document type is required.", nameof(request.DocumentType));
        }

        if (string.IsNullOrWhiteSpace(request.DocumentNumber))
        {
            throw new ArgumentException("Document number is required.", nameof(request.DocumentNumber));
        }

        // Mask document number for security e.g. ***4589
        var cleanDoc = request.DocumentNumber.Trim();
        var maskedDoc = cleanDoc.Length > 4
            ? new string('*', cleanDoc.Length - 4) + cleanDoc[^4..]
            : new string('*', cleanDoc.Length);

        account.KycDocumentType = request.DocumentType.Trim();
        account.KycDocumentNumber = maskedDoc;
        account.KycStatus = request.AutoApprove ? KycStatus.Verified : KycStatus.Pending;
        account.UpdatedAt = DateTime.UtcNow;

        if (account.KycStatus == KycStatus.Verified)
        {
            // Upgrade transaction limits upon KYC verification
            if (account.AccountType == AccountType.Business)
            {
                account.DailyTransferLimit = 250000.00m;
                account.SingleTransferLimit = 100000.00m;
            }
            else
            {
                account.DailyTransferLimit = 50000.00m;
                account.SingleTransferLimit = 15000.00m;
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Account {AccountNumber} KYC verified: Status={Status}", account.AccountNumber, account.KycStatus);

        return account;
    }

    public async Task<Account> UpdateStatusAsync(int accountId, AccountStatus status, string? reason)
    {
        var account = await _db.Accounts.FindAsync(accountId)
            ?? throw new KeyNotFoundException($"Account ID {accountId} not found.");

        account.Status = status;
        account.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Account {AccountNumber} status updated to {Status}. Reason: {Reason}", account.AccountNumber, status, reason ?? "N/A");

        return account;
    }

    public async Task<Account> UpdateLimitsAsync(int accountId, decimal dailyLimit, decimal singleLimit)
    {
        if (dailyLimit <= 0 || singleLimit <= 0)
        {
            throw new ArgumentException("Limits must be strictly positive.");
        }
        if (singleLimit > dailyLimit)
        {
            throw new ArgumentException("Single transfer limit cannot exceed daily transfer limit.");
        }

        var account = await _db.Accounts.FindAsync(accountId)
            ?? throw new KeyNotFoundException($"Account ID {accountId} not found.");

        account.DailyTransferLimit = dailyLimit;
        account.SingleTransferLimit = singleLimit;
        account.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return account;
    }

    public async Task<List<Account>> GetAllAccountsAsync(bool includeInternal = false)
    {
        var query = _db.Accounts.AsNoTracking();
        if (!includeInternal)
        {
            query = query.Where(a => a.AccountType != AccountType.VaultReserve);
        }

        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync();
    }

    public async Task<Account?> GetAccountByIdAsync(int id)
    {
        return await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Account?> GetAccountByNumberAsync(string accountNumber)
    {
        return await _db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.AccountNumber == accountNumber);
    }
}
