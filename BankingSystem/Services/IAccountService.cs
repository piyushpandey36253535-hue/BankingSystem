using BankingSystem.Models;

namespace BankingSystem.Services;

public interface IAccountService
{
    Task<Account> CreateAccountAsync(CreateAccountRequest request);
    Task<Account> VerifyKycAsync(int accountId, VerifyKycRequest request);
    Task<Account> UpdateStatusAsync(int accountId, AccountStatus status, string? reason);
    Task<Account> UpdateLimitsAsync(int accountId, decimal dailyLimit, decimal singleLimit);
    Task<List<Account>> GetAllAccountsAsync(bool includeInternal = false);
    Task<Account?> GetAccountByIdAsync(int id);
    Task<Account?> GetAccountByNumberAsync(string accountNumber);
}
