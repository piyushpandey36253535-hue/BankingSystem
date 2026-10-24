using BankingSystem.Models;

namespace BankingSystem.Services;

public interface ILedgerService
{
    Task<LedgerJournal> PostJournalAsync(
        TransactionType transactionType,
        decimal amount,
        string description,
        string referenceNumber,
        int? sourceAccountId,
        int? destinationAccountId,
        string? externalInstitution = null,
        string? externalRouting = null,
        string? externalAccount = null,
        bool isDeposit = false);

    Task<LedgerJournal> ReverseJournalAsync(int journalId, string reason, string reversedBy);
    Task<List<LedgerJournal>> GetJournalsAsync(int page = 1, int pageSize = 50, int? accountId = null);
    Task<LedgerJournal?> GetJournalByIdAsync(int journalId);
    Task<(decimal totalDebits, decimal totalCredits, bool isBalanced)> VerifyTrialBalanceAsync();
}
