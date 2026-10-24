using BankingSystem.Models;

namespace BankingSystem.Services;

public record TransferExecutionResult(
    bool Success,
    string Message,
    int? JournalId,
    string? ReferenceNumber,
    bool HeldForFraudReview,
    int? FraudAlertId,
    int RiskScore,
    decimal Amount
);

public interface ITransferService
{
    Task<TransferExecutionResult> ExecuteTransferAsync(InitiateTransferRequest request);
    Task<LedgerJournal> SimulateFaucetDepositAsync(SimulateFaucetDepositRequest request);
    Task<ScheduledTransfer> CreateScheduledTransferAsync(CreateScheduledTransferRequest request);
    Task<List<ScheduledTransfer>> GetScheduledTransfersAsync(int? accountId = null);
    Task<bool> CancelScheduledTransferAsync(int scheduledTransferId);
}
