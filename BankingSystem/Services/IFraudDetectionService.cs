using BankingSystem.Models;

namespace BankingSystem.Services;

public record FraudEvaluationResult(
    bool IsAllowed,
    bool RequiresReview,
    int RiskScore,
    AlertSeverity Severity,
    List<string> TriggeredRules,
    string Details
);

public interface IFraudDetectionService
{
    Task<FraudEvaluationResult> EvaluateTransferAsync(
        Account sourceAccount,
        decimal amount,
        string? externalInstitution,
        bool isInterbank);

    Task<FraudAlert> CreateAlertAsync(
        int accountId,
        int? journalId,
        decimal flaggedAmount,
        FraudEvaluationResult evaluation);

    Task<FraudAlert> ReviewAlertAsync(int alertId, ReviewFraudAlertRequest request);
    Task<List<FraudAlert>> GetAlertsAsync(string? status = null, int? accountId = null);
    Task<FraudAlert?> GetAlertByIdAsync(int alertId);
    Task<List<FraudAlert>> SimulateAttackScenarioAsync(int accountId, string scenarioType);
}
