namespace BankingSystem.Models;

public enum AccountType
{
    Checking = 1,
    Savings = 2,
    Business = 3,
    VaultReserve = 99 // Internal Bank reserve asset account
}

public enum AccountStatus
{
    Active = 1,
    Frozen = 2,
    Suspended = 3,
    Closed = 4
}

public enum KycStatus
{
    Unverified = 0,
    Pending = 1,
    Verified = 2,
    Rejected = 3
}

public enum TransactionType
{
    Deposit = 1,
    Withdrawal = 2,
    IntraBankTransfer = 3,
    InterBankTransfer = 4,
    Fee = 5,
    InterestPayment = 6,
    Reversal = 7
}

public enum JournalStatus
{
    Pending = 1,
    Posted = 2,
    FlaggedForReview = 3,
    Held = 4,
    Rejected = 5,
    Reversed = 6
}

public enum EntryType
{
    Debit = 1,
    Credit = 2
}

public enum AlertSeverity
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum FraudAlertStatus
{
    Open = 1,
    UnderReview = 2,
    Approved = 3,
    Rejected = 4,
    AutoBlocked = 5
}

public enum ScheduleFrequency
{
    Once = 1,
    Daily = 2,
    Weekly = 3,
    Monthly = 4
}

public enum ReconciliationStatus
{
    Passed = 1,
    DiscrepancyDetected = 2,
    Error = 3
}
