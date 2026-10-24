using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using BankingSystem.Data;
using BankingSystem.Models;
using BankingSystem.Services;
using Xunit;

namespace BankingSystem.Tests;

public class BankingSystemTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly BankingDbContext _db;
    private readonly ILedgerService _ledgerService;
    private readonly IAccountService _accountService;
    private readonly IFraudDetectionService _fraudService;
    private readonly ITransferService _transferService;
    private readonly IStatementService _statementService;
    private readonly IReconciliationService _reconciliationService;

    public BankingSystemTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<BankingDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new BankingDbContext(options);
        _db.Database.EnsureCreated();

        // Seed system vault and base accounts
        DbInitializer.InitializeAsync(_db).GetAwaiter().GetResult();

        _ledgerService = new LedgerService(_db, NullLogger<LedgerService>.Instance);
        _accountService = new AccountService(_db, _ledgerService, NullLogger<AccountService>.Instance);
        _fraudService = new FraudDetectionService(_db, NullLogger<FraudDetectionService>.Instance);
        _transferService = new TransferService(_db, _ledgerService, _fraudService, NullLogger<TransferService>.Instance);
        _statementService = new StatementService(_db);
        _reconciliationService = new ReconciliationService(_db, NullLogger<ReconciliationService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Test_AccountCreation_And_KycLimitElevation()
    {
        // 1. Create personal checking account
        var request = new CreateAccountRequest(
            AccountHolderName: "Julian Barnes",
            Email: "julian.b@example.com",
            Phone: "+1 (555) 912-3456",
            AccountType: AccountType.Checking,
            InitialDeposit: 500.00m
        );

        var account = await _accountService.CreateAccountAsync(request);

        Assert.NotNull(account);
        Assert.StartsWith("ACCT-", account.AccountNumber);
        Assert.Equal(500.00m, account.Balance);
        Assert.Equal(500.00m, account.AvailableBalance);
        Assert.Equal(KycStatus.Unverified, account.KycStatus);
        Assert.Equal(2500.00m, account.DailyTransferLimit);

        // 2. Submit KYC verification
        var verifyRequest = new VerifyKycRequest(
            DocumentType: "Passport",
            DocumentNumber: "USA-92841029",
            AutoApprove: true
        );

        var verifiedAccount = await _accountService.VerifyKycAsync(account.Id, verifyRequest);

        Assert.Equal(KycStatus.Verified, verifiedAccount.KycStatus);
        Assert.Equal(50000.00m, verifiedAccount.DailyTransferLimit); // Elevated
        Assert.Equal(15000.00m, verifiedAccount.SingleTransferLimit); // Elevated
    }

    [Fact]
    public async Task Test_Strict_DoubleEntry_Deposit_And_TrialBalance()
    {
        var customer = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-8391048201"); // Elena
        var startingBalance = customer.Balance;

        // Execute Faucet Deposit
        var journal = await _transferService.SimulateFaucetDepositAsync(new SimulateFaucetDepositRequest(
            AccountId: customer.Id,
            Amount: 10000.00m,
            Note: "Test Capital Inflow"
        ));

        Assert.NotNull(journal);
        Assert.Equal(JournalStatus.Posted, journal.Status);

        // Reload customer
        await _db.Entry(customer).ReloadAsync();
        Assert.Equal(startingBalance + 10000.00m, customer.Balance);

        // Verify General Ledger Trial Balance: Total Debits must exactly equal Total Credits
        var (totalDebits, totalCredits, isBalanced) = await _ledgerService.VerifyTrialBalanceAsync();
        Assert.True(isBalanced);
        Assert.Equal(totalDebits, totalCredits);
    }

    [Fact]
    public async Task Test_IntraBankTransfer_Execution_And_BalanceUpdates()
    {
        var elena = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-8391048201");
        var marcus = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-9182736450");

        var elenaInitial = elena.Balance;
        var marcusInitial = marcus.Balance;
        const decimal transferAmount = 2500.00m;

        var result = await _transferService.ExecuteTransferAsync(new InitiateTransferRequest(
            SourceAccountId: elena.Id,
            DestinationAccountId: marcus.Id,
            ExternalAccountNumber: null,
            ExternalRoutingOrSwift: null,
            ExternalInstitutionName: null,
            Amount: transferAmount,
            Description: "Retainer settlement",
            IsInterBank: false
        ));

        Assert.True(result.Success);
        Assert.False(result.HeldForFraudReview);

        await _db.Entry(elena).ReloadAsync();
        await _db.Entry(marcus).ReloadAsync();

        Assert.Equal(elenaInitial - transferAmount, elena.Balance);
        Assert.Equal(marcusInitial + transferAmount, marcus.Balance);

        // Trial balance check
        var (_, _, isBalanced) = await _ledgerService.VerifyTrialBalanceAsync();
        Assert.True(isBalanced);
    }

    [Fact]
    public async Task Test_FraudDetection_Structuring_HoldsTransfer()
    {
        // Structuring rule flags transfers between $9,000 and $9,999.99
        var corp = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-7482910392");
        var marcus = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-9182736450");

        var initialAvailable = corp.AvailableBalance;
        var initialBalance = corp.Balance;
        const decimal structuringAmount = 9850.00m;

        var result = await _transferService.ExecuteTransferAsync(new InitiateTransferRequest(
            SourceAccountId: corp.Id,
            DestinationAccountId: marcus.Id,
            ExternalAccountNumber: null,
            ExternalRoutingOrSwift: null,
            ExternalInstitutionName: null,
            Amount: structuringAmount,
            Description: "Payment split under 10k",
            IsInterBank: false
        ));

        // Transfer should be placed on HOLD
        Assert.False(result.Success);
        Assert.True(result.HeldForFraudReview);
        Assert.NotNull(result.FraudAlertId);
        Assert.True(result.RiskScore >= 60);

        await _db.Entry(corp).ReloadAsync();
        // Balance remains unchanged until reviewed, but AvailableBalance is placed on hold
        Assert.Equal(initialBalance, corp.Balance);
        Assert.Equal(initialAvailable - structuringAmount, corp.AvailableBalance);
    }

    [Fact]
    public async Task Test_FraudReview_Approval_ReleasesHold_And_Posts()
    {
        var corp = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-7482910392");
        var marcus = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-9182736450");

        const decimal amount = 9850.00m;

        var transferResult = await _transferService.ExecuteTransferAsync(new InitiateTransferRequest(
            SourceAccountId: corp.Id,
            DestinationAccountId: marcus.Id,
            ExternalAccountNumber: null,
            ExternalRoutingOrSwift: null,
            ExternalInstitutionName: null,
            Amount: amount,
            Description: "Structuring test for review",
            IsInterBank: false
        ));

        Assert.True(transferResult.HeldForFraudReview);
        var alertId = transferResult.FraudAlertId!.Value;

        // Compliance Officer reviews and approves
        var reviewedAlert = await _fraudService.ReviewAlertAsync(alertId, new ReviewFraudAlertRequest(
            Decision: FraudAlertStatus.Approved,
            ReviewNotes: "Verified bona fide corporate invoice documentation.",
            ReviewedBy: "Chief Compliance Officer",
            FreezeAccountIfRejected: false
        ));

        Assert.Equal(FraudAlertStatus.Approved, reviewedAlert.Status);

        // Verify Journal is now Posted and balance deducted
        var journal = await _db.LedgerJournals.FindAsync(transferResult.JournalId);
        Assert.NotNull(journal);
        Assert.Equal(JournalStatus.Posted, journal.Status);

        await _db.Entry(corp).ReloadAsync();
        await _db.Entry(marcus).ReloadAsync();

        // Trial balance check
        var (_, _, isBalanced) = await _ledgerService.VerifyTrialBalanceAsync();
        Assert.True(isBalanced);
    }

    [Fact]
    public async Task Test_Reversal_ReversesEntries_And_PreservesTrialBalance()
    {
        var elena = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-8391048201");
        var marcus = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-9182736450");

        var elenaPre = elena.Balance;
        var marcusPre = marcus.Balance;

        // Post transfer
        var result = await _transferService.ExecuteTransferAsync(new InitiateTransferRequest(
            SourceAccountId: elena.Id,
            DestinationAccountId: marcus.Id,
            ExternalAccountNumber: null,
            ExternalRoutingOrSwift: null,
            ExternalInstitutionName: null,
            Amount: 1200.00m,
            Description: "Mistaken wire transfer",
            IsInterBank: false
        ));

        var journalId = result.JournalId!.Value;

        // Reverse the journal
        var reversal = await _ledgerService.ReverseJournalAsync(journalId, "Client canceled order", "Auditor Jane");
        Assert.NotNull(reversal);
        Assert.Equal(TransactionType.Reversal, reversal.TransactionType);

        await _db.Entry(elena).ReloadAsync();
        await _db.Entry(marcus).ReloadAsync();

        // Balances restored to pre-transfer state
        Assert.Equal(elenaPre, elena.Balance);
        Assert.Equal(marcusPre, marcus.Balance);

        // Trial balance strictly verified
        var (_, _, isBalanced) = await _ledgerService.VerifyTrialBalanceAsync();
        Assert.True(isBalanced);
    }

    [Fact]
    public async Task Test_Reconciliation_ZeroDiscrepancy()
    {
        var record = await _reconciliationService.RunReconciliationAuditAsync();

        Assert.NotNull(record);
        Assert.Equal(ReconciliationStatus.Passed, record.Status);
        Assert.True(record.IsLedgerBalanced);
        Assert.Equal(0, record.DiscrepancyCount);
        Assert.Equal(0.00m, record.TotalDiscrepancyAmount);
    }

    [Fact]
    public async Task Test_Statement_Generation()
    {
        var elena = await _db.Accounts.FirstAsync(a => a.AccountNumber == "ACCT-8391048201");

        var statement = await _statementService.GenerateStatementAsync(elena.Id);

        Assert.NotNull(statement);
        Assert.Equal(elena.AccountNumber, statement.AccountNumber);
        Assert.Equal(elena.Balance, statement.EndingBalance);
        Assert.True(statement.TransactionCount > 0);

        // Also test HTML printable generator
        var html = await _statementService.GenerateHtmlStatementAsync(elena.Id);
        Assert.Contains("CANADA CORE BANKING SYSTEM", html);
        Assert.Contains(elena.AccountNumber, html);
    }
}
