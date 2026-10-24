using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;
using BankingSystem.Services;

namespace BankingSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly BankingDbContext _db;
    private readonly ILedgerService _ledgerService;
    private readonly IReconciliationService _reconciliationService;

    public AnalyticsController(
        BankingDbContext db,
        ILedgerService ledgerService,
        IReconciliationService reconciliationService)
    {
        _db = db;
        _ledgerService = ledgerService;
        _reconciliationService = reconciliationService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<SystemAnalyticsDto>> GetOverview()
    {
        var activeDepositBalances = await _db.Accounts
            .Where(a => a.AccountType != AccountType.VaultReserve && a.Status == AccountStatus.Active)
            .Select(a => a.Balance)
            .ToListAsync();
        var totalDeposits = activeDepositBalances.Sum();

        // 2. Vault Reserve Asset Balance
        var vault = await _db.Accounts.FirstOrDefaultAsync(a => a.AccountNumber == "VAULT-RESERVE-01");
        var vaultBalance = vault?.Balance ?? 0.00m;

        // 3. 24h Transfer Volume
        var oneDayAgo = DateTime.UtcNow.AddDays(-1);
        var vol24hAmounts = await _db.LedgerJournals
            .Where(j => j.Timestamp >= oneDayAgo && j.Status == JournalStatus.Posted && (j.TransactionType == TransactionType.IntraBankTransfer || j.TransactionType == TransactionType.InterBankTransfer))
            .Select(j => j.Amount)
            .ToListAsync();
        var vol24h = vol24hAmounts.Sum();

        // 4. Counts
        var activeAccounts = await _db.Accounts.CountAsync(a => a.AccountType != AccountType.VaultReserve && a.Status == AccountStatus.Active);
        var pendingKyc = await _db.Accounts.CountAsync(a => a.KycStatus == KycStatus.Pending || a.KycStatus == KycStatus.Unverified);
        var openAlerts = await _db.FraudAlerts.CountAsync(a => a.Status == FraudAlertStatus.Open || a.Status == FraudAlertStatus.UnderReview);
        var totalTxns = await _db.LedgerJournals.CountAsync(j => j.Status == JournalStatus.Posted);

        // 5. Ledger Integrity
        var trialBalance = await _ledgerService.VerifyTrialBalanceAsync();

        // 6. Latest Reconciliation
        var latestRecon = await _reconciliationService.GetLatestReconciliationAsync();

        return Ok(new SystemAnalyticsDto(
            TotalDepositsAcrossAccounts: totalDeposits,
            VaultReserveAssetBalance: vaultBalance,
            Total24HourTransferVolume: vol24h,
            TotalActiveAccounts: activeAccounts,
            PendingKycCount: pendingKyc,
            OpenFraudAlertsCount: openAlerts,
            IsLedgerCurrentlyBalanced: trialBalance.isBalanced,
            LastReconciledAt: latestRecon?.AuditTimestamp ?? DateTime.UtcNow,
            TotalCompletedTransactions: totalTxns
        ));
    }
}
