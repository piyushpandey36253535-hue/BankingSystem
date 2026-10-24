using Microsoft.EntityFrameworkCore;
using BankingSystem.Models;

namespace BankingSystem.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(BankingDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        // Check if already seeded
        if (await db.Accounts.AnyAsync())
        {
            return;
        }

        var now = DateTime.UtcNow;

        // 1. Create Internal System Accounts
        var vault = new Account
        {
            AccountNumber = "VAULT-RESERVE-01",
            RoutingNumber = "021000021",
            AccountHolderName = "Canada Central Vault & Reserves",
            Email = "treasury@canadabank.internal",
            Phone = "+1 (800) 555-0100",
            AccountType = AccountType.VaultReserve,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Verified,
            Balance = 10571650.00m,
            AvailableBalance = 10571650.00m,
            DailyTransferLimit = 10000000.00m,
            SingleTransferLimit = 10000000.00m,
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now
        };

        var clearing = new Account
        {
            AccountNumber = "CLEARING-ACH-01",
            RoutingNumber = "021000021",
            AccountHolderName = "ACH & Fedwire Clearing Account",
            Email = "clearing@canadabank.internal",
            Phone = "+1 (800) 555-0101",
            AccountType = AccountType.VaultReserve,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Verified,
            Balance = 2500000.00m,
            AvailableBalance = 2500000.00m,
            DailyTransferLimit = 5000000.00m,
            SingleTransferLimit = 5000000.00m,
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now
        };

        var capitalEquity = new Account
        {
            AccountNumber = "BANK-CAPITAL-EQUITY",
            RoutingNumber = "021000021",
            AccountHolderName = "Paid-In Capital & Bank Reserves",
            Email = "capital@canadabank.internal",
            Phone = "+1 (800) 555-0102",
            AccountType = AccountType.VaultReserve,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Verified,
            Balance = 12500000.00m,
            AvailableBalance = 12500000.00m,
            DailyTransferLimit = 10000000.00m,
            SingleTransferLimit = 10000000.00m,
            CreatedAt = now.AddDays(-30),
            UpdatedAt = now
        };

        // 2. Create Retail & Commercial Customer Accounts
        var businessAccount = new Account
        {
            AccountNumber = "ACCT-7482910392",
            RoutingNumber = "021000021",
            AccountHolderName = "Canada Enterprise Holdings LLC",
            Email = "finance@canadaenterprise.com",
            Phone = "+1 (212) 555-0199",
            AccountType = AccountType.Business,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Verified,
            KycDocumentType = "Certificate of Incorporation",
            KycDocumentNumber = "****9842",
            Balance = 450000.00m,
            AvailableBalance = 450000.00m,
            DailyTransferLimit = 250000.00m,
            SingleTransferLimit = 100000.00m,
            CreatedAt = now.AddDays(-20),
            UpdatedAt = now
        };

        var customer1 = new Account
        {
            AccountNumber = "ACCT-8391048201",
            RoutingNumber = "021000021",
            AccountHolderName = "Elena Rostova",
            Email = "elena.rostova@example.com",
            Phone = "+1 (415) 555-0182",
            AccountType = AccountType.Checking,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Verified,
            KycDocumentType = "Passport",
            KycDocumentNumber = "****3291",
            Balance = 33000.00m,
            AvailableBalance = 33000.00m,
            DailyTransferLimit = 50000.00m,
            SingleTransferLimit = 15000.00m,
            CreatedAt = now.AddDays(-15),
            UpdatedAt = now
        };

        var customer2 = new Account
        {
            AccountNumber = "ACCT-9182736450",
            RoutingNumber = "021000021",
            AccountHolderName = "Marcus Vance",
            Email = "marcus.vance@example.com",
            Phone = "+1 (312) 555-0144",
            AccountType = AccountType.Savings,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Verified,
            KycDocumentType = "Driver License",
            KycDocumentNumber = "****6110",
            Balance = 83500.00m,
            AvailableBalance = 83500.00m,
            DailyTransferLimit = 50000.00m,
            SingleTransferLimit = 15000.00m,
            CreatedAt = now.AddDays(-12),
            UpdatedAt = now
        };

        var customer3 = new Account
        {
            AccountNumber = "ACCT-1928374651",
            RoutingNumber = "021000021",
            AccountHolderName = "Sophia Chen",
            Email = "sophia.chen@example.com",
            Phone = "+1 (206) 555-0177",
            AccountType = AccountType.Checking,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Pending,
            KycDocumentType = "National ID",
            KycDocumentNumber = "****7843",
            Balance = 4200.00m,
            AvailableBalance = 4200.00m,
            DailyTransferLimit = 2500.00m,
            SingleTransferLimit = 1000.00m,
            CreatedAt = now.AddDays(-5),
            UpdatedAt = now
        };

        var customer4 = new Account
        {
            AccountNumber = "ACCT-5647382910",
            RoutingNumber = "021000021",
            AccountHolderName = "David Miller",
            Email = "david.miller@example.com",
            Phone = "+1 (512) 555-0123",
            AccountType = AccountType.Checking,
            Status = AccountStatus.Active,
            KycStatus = KycStatus.Unverified,
            Balance = 950.00m,
            AvailableBalance = 950.00m,
            DailyTransferLimit = 1000.00m,
            SingleTransferLimit = 500.00m,
            CreatedAt = now.AddDays(-2),
            UpdatedAt = now
        };

        db.Accounts.AddRange(vault, clearing, capitalEquity, businessAccount, customer1, customer2, customer3, customer4);
        await db.SaveChangesAsync();

        // 3. Seed Strict Double-Entry Ledger Journals and Entries
        // To maintain perfect ledger balance:
        // Every customer deposit has:
        // Debit: Vault (+Asset)
        // Credit: Customer Account (+Liability)
        var journals = new List<LedgerJournal>();
        var entries = new List<LedgerEntry>();

        // 3a. Initial Capitalization Journals (Assets = Equity)
        var capJournal1 = new LedgerJournal
        {
            JournalNumber = $"JRN-{now.AddDays(-30):yyyyMMdd}-CAP01",
            ReferenceNumber = "CAP-ENDOW-VAULT-01",
            TransactionType = TransactionType.Deposit,
            Status = JournalStatus.Posted,
            Amount = 10000000.00m,
            Description = "Canada Central Vault Reserve Capital Endowment",
            SourceAccountId = null,
            DestinationAccountId = vault.Id,
            Timestamp = now.AddDays(-30)
        };
        journals.Add(capJournal1);

        entries.Add(new LedgerEntry
        {
            Journal = capJournal1,
            AccountId = vault.Id,
            EntryType = EntryType.Debit,
            Amount = 10000000.00m,
            RunningBalanceAfter = 10000000.00m,
            Timestamp = now.AddDays(-30),
            Note = "Initial vault asset capitalization"
        });

        entries.Add(new LedgerEntry
        {
            Journal = capJournal1,
            AccountId = capitalEquity.Id,
            EntryType = EntryType.Credit,
            Amount = 10000000.00m,
            RunningBalanceAfter = 10000000.00m,
            Timestamp = now.AddDays(-30),
            Note = "Paid-in capital equity credit"
        });

        var capJournal2 = new LedgerJournal
        {
            JournalNumber = $"JRN-{now.AddDays(-30):yyyyMMdd}-CAP02",
            ReferenceNumber = "CAP-ENDOW-CLEAR-01",
            TransactionType = TransactionType.Deposit,
            Status = JournalStatus.Posted,
            Amount = 2500000.00m,
            Description = "ACH & Fedwire Clearing Account Settlement Reserve",
            SourceAccountId = null,
            DestinationAccountId = clearing.Id,
            Timestamp = now.AddDays(-30)
        };
        journals.Add(capJournal2);

        entries.Add(new LedgerEntry
        {
            Journal = capJournal2,
            AccountId = clearing.Id,
            EntryType = EntryType.Debit,
            Amount = 2500000.00m,
            RunningBalanceAfter = 2500000.00m,
            Timestamp = now.AddDays(-30),
            Note = "Initial clearing settlement asset"
        });

        entries.Add(new LedgerEntry
        {
            Journal = capJournal2,
            AccountId = capitalEquity.Id,
            EntryType = EntryType.Credit,
            Amount = 2500000.00m,
            RunningBalanceAfter = 12500000.00m,
            Timestamp = now.AddDays(-30),
            Note = "Paid-in capital equity credit for clearing"
        });

        void AddBalancedDeposit(Account customer, decimal amount, string refCode, string desc, DateTime timestamp)
        {
            var journal = new LedgerJournal
            {
                JournalNumber = $"JRN-{timestamp:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
                ReferenceNumber = refCode,
                TransactionType = TransactionType.Deposit,
                Status = JournalStatus.Posted,
                Amount = amount,
                Description = desc,
                SourceAccountId = null,
                DestinationAccountId = customer.Id,
                Timestamp = timestamp
            };
            journals.Add(journal);

            entries.Add(new LedgerEntry
            {
                Journal = journal,
                AccountId = vault.Id,
                EntryType = EntryType.Debit,
                Amount = amount,
                RunningBalanceAfter = vault.Balance,
                Timestamp = timestamp,
                Note = $"Cash deposit reserve inflow for {customer.AccountNumber}"
            });

            entries.Add(new LedgerEntry
            {
                Journal = journal,
                AccountId = customer.Id,
                EntryType = EntryType.Credit,
                Amount = amount,
                RunningBalanceAfter = customer.Balance,
                Timestamp = timestamp,
                Note = "Customer initial balance deposit"
            });
        }

        AddBalancedDeposit(businessAccount, 450000.00m, "TXN-INIT-CORP-01", "Corporate Treasury Initial Inflow", now.AddDays(-20));
        AddBalancedDeposit(customer1, 34500.00m, "TXN-INIT-ELENA-01", "Payroll Direct Deposit", now.AddDays(-15));
        AddBalancedDeposit(customer2, 82000.00m, "TXN-INIT-MARCUS-01", "High-Yield Savings Deposit", now.AddDays(-12));
        AddBalancedDeposit(customer3, 4200.00m, "TXN-INIT-SOPHIA-01", "Mobile Check Deposit", now.AddDays(-5));
        AddBalancedDeposit(customer4, 950.00m, "TXN-INIT-DAVID-01", "Initial Cash Deposit", now.AddDays(-2));

        // Seed a sample intra-bank transfer between Elena and Marcus
        var transferJournal = new LedgerJournal
        {
            JournalNumber = $"JRN-{now.AddDays(-3):yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            ReferenceNumber = "TXN-SEED-XFER-01",
            TransactionType = TransactionType.IntraBankTransfer,
            Status = JournalStatus.Posted,
            Amount = 1500.00m,
            Description = "Consulting Services Payment",
            SourceAccountId = customer1.Id,
            DestinationAccountId = customer2.Id,
            Timestamp = now.AddDays(-3)
        };
        journals.Add(transferJournal);

        entries.Add(new LedgerEntry
        {
            Journal = transferJournal,
            AccountId = customer1.Id,
            EntryType = EntryType.Debit,
            Amount = 1500.00m,
            RunningBalanceAfter = 33000.00m,
            Timestamp = now.AddDays(-3),
            Note = $"Transfer outflow to {customer2.AccountNumber}"
        });

        entries.Add(new LedgerEntry
        {
            Journal = transferJournal,
            AccountId = customer2.Id,
            EntryType = EntryType.Credit,
            Amount = 1500.00m,
            RunningBalanceAfter = 83500.00m,
            Timestamp = now.AddDays(-3),
            Note = $"Transfer inflow from {customer1.AccountNumber}"
        });

        db.LedgerJournals.AddRange(journals);
        db.LedgerEntries.AddRange(entries);
        await db.SaveChangesAsync();

        // 4. Seed an Active Fraud Alert to showcase compliance console
        var fraudAlert = new FraudAlert
        {
            AccountId = customer4.Id,
            FlaggedAmount = 9850.00m,
            RiskScore = 85,
            Severity = AlertSeverity.High,
            Status = FraudAlertStatus.Open,
            TriggeredRules = "STRUCTURING_AML_SUSPECT, SINGLE_LIMIT_EXCEEDED",
            Details = "Transaction amount $9,850.00 flagged for potential AML structuring evasion just under $10,000 threshold and exceeding unverified tier limit ($500.00).",
            CreatedAt = now.AddHours(-2)
        };
        db.FraudAlerts.Add(fraudAlert);

        // 5. Seed a Scheduled Transfer
        var scheduledTransfer = new ScheduledTransfer
        {
            SourceAccountId = customer1.Id,
            DestinationAccountId = customer2.Id,
            Amount = 250.00m,
            Description = "Monthly Shared Investment Contribution",
            Frequency = ScheduleFrequency.Monthly,
            NextExecutionDate = now.AddDays(10),
            IsActive = true,
            CreatedAt = now.AddDays(-2)
        };
        db.ScheduledTransfers.Add(scheduledTransfer);

        // 6. Seed initial Reconciliation Audit record
        var totalDebits = entries.Where(e => e.EntryType == EntryType.Debit).Sum(e => e.Amount);
        var totalCredits = entries.Where(e => e.EntryType == EntryType.Credit).Sum(e => e.Amount);

        var reconciliation = new ReconciliationRecord
        {
            AuditTimestamp = now.AddMinutes(-10),
            TotalAccountsAudited = 8,
            TotalLedgerDebits = totalDebits,
            TotalLedgerCredits = totalCredits,
            IsLedgerBalanced = true,
            TotalAccountBalancesSum = vault.Balance + clearing.Balance + capitalEquity.Balance + businessAccount.Balance + customer1.Balance + customer2.Balance + customer3.Balance + customer4.Balance,
            TotalDiscrepancyAmount = 0.00m,
            DiscrepancyCount = 0,
            Status = ReconciliationStatus.Passed,
            SummaryMessage = $"Genesis Ledger Reconciliation: 8 accounts audited. Total Debits (${totalDebits:N2}) == Total Credits (${totalCredits:N2}). Zero variance."
        };
        db.ReconciliationRecords.Add(reconciliation);

        await db.SaveChangesAsync();
    }
}
