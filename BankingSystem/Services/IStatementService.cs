using BankingSystem.Models;

namespace BankingSystem.Services;

public interface IStatementService
{
    Task<StatementSummaryDto> GenerateStatementAsync(int accountId, DateTime? startDate = null, DateTime? endDate = null);
    Task<string> GenerateHtmlStatementAsync(int accountId, DateTime? startDate = null, DateTime? endDate = null);
}
