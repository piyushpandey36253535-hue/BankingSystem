namespace BankingSystem.Models;

public record CreateAccountRequest(
    string AccountHolderName,
    string Email,
    string Phone,
    AccountType AccountType,
    decimal InitialDeposit = 0.00m
);

public record VerifyKycRequest(
    string DocumentType, // "Passport", "DriverLicense", "NationalId"
    string DocumentNumber,
    bool AutoApprove = true
);

public record UpdateLimitsRequest(
    decimal DailyLimit,
    decimal SingleLimit
);

public record UpdateStatusRequest(
    AccountStatus Status,
    string? Reason
);

public record InitiateTransferRequest(
    int SourceAccountId,
    int? DestinationAccountId,
    string? ExternalAccountNumber,
    string? ExternalRoutingOrSwift,
    string? ExternalInstitutionName,
    decimal Amount,
    string Description,
    bool IsInterBank = false
);

public record SimulateFaucetDepositRequest(
    int AccountId,
    decimal Amount,
    string Note = "Simulated Cash Inflow / Faucet Deposit"
);

public record ReviewFraudAlertRequest(
    FraudAlertStatus Decision, // Approved, Rejected
    string ReviewNotes,
    string ReviewedBy,
    bool FreezeAccountIfRejected = false
);

public record CreateScheduledTransferRequest(
    int SourceAccountId,
    int DestinationAccountId,
    decimal Amount,
    string Description,
    ScheduleFrequency Frequency,
    DateTime StartDate
);

public record AccountDto(
    int Id,
    string AccountNumber,
    string RoutingNumber,
    string AccountHolderName,
    string Email,
    string Phone,
    string AccountType,
    string Status,
    string KycStatus,
    string? KycDocumentType,
    string? KycDocumentNumber,
    string Currency,
    decimal Balance,
    decimal AvailableBalance,
    decimal DailyTransferLimit,
    decimal SingleTransferLimit,
    DateTime CreatedAt
);

public record LedgerEntryDto(
    long Id,
    int LedgerJournalId,
    int AccountId,
    string AccountNumber,
    string AccountHolderName,
    string EntryType,
    decimal Amount,
    decimal RunningBalanceAfter,
    DateTime Timestamp,
    string? Note
);

public record LedgerJournalDto(
    int Id,
    string JournalNumber,
    string ReferenceNumber,
    string TransactionType,
    string Status,
    decimal Amount,
    string Description,
    int? SourceAccountId,
    string? SourceAccountNumber,
    string? SourceHolderName,
    int? DestinationAccountId,
    string? DestinationAccountNumber,
    string? DestinationHolderName,
    string? ExternalInstitutionName,
    DateTime Timestamp,
    List<LedgerEntryDto> Entries,
    int? FraudAlertId,
    int? FraudRiskScore
);

public record FraudAlertDto(
    int Id,
    int? LedgerJournalId,
    string? JournalReference,
    int AccountId,
    string AccountNumber,
    string AccountHolderName,
    int RiskScore,
    string Severity,
    string Status,
    string TriggeredRules,
    string Details,
    decimal FlaggedAmount,
    string? ReviewNotes,
    string? ReviewedBy,
    DateTime CreatedAt,
    DateTime? ResolvedAt
);

public record StatementSummaryDto(
    int AccountId,
    string AccountNumber,
    string AccountHolderName,
    string AccountType,
    string Currency,
    DateTime StartDate,
    DateTime EndDate,
    decimal StartingBalance,
    decimal TotalDeposits,
    decimal TotalWithdrawals,
    decimal EndingBalance,
    int TransactionCount,
    List<StatementLineDto> Transactions
);

public record StatementLineDto(
    DateTime Date,
    string ReferenceNumber,
    string Description,
    string TransactionType,
    string Direction, // Inflow / Outflow
    decimal Amount,
    decimal BalanceAfter
);

public record ReconciliationSummaryDto(
    int Id,
    DateTime AuditTimestamp,
    int TotalAccountsAudited,
    decimal TotalLedgerDebits,
    decimal TotalLedgerCredits,
    bool IsLedgerBalanced,
    decimal TotalAccountBalancesSum,
    decimal TotalDiscrepancyAmount,
    int DiscrepancyCount,
    string Status,
    string SummaryMessage,
    string? DiscrepancyDetailsJson
);

public record SystemAnalyticsDto(
    decimal TotalDepositsAcrossAccounts,
    decimal VaultReserveAssetBalance,
    decimal Total24HourTransferVolume,
    int TotalActiveAccounts,
    int PendingKycCount,
    int OpenFraudAlertsCount,
    bool IsLedgerCurrentlyBalanced,
    DateTime LastReconciledAt,
    int TotalCompletedTransactions
);
