using BankingSystem.Models;

namespace BankingSystem.Services;

public interface IReconciliationService
{
    Task<ReconciliationRecord> RunReconciliationAuditAsync();
    Task<List<ReconciliationRecord>> GetReconciliationHistoryAsync(int count = 20);
    Task<ReconciliationRecord?> GetLatestReconciliationAsync();
}
