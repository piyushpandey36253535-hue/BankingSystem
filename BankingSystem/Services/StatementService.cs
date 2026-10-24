using System.Text;
using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;

namespace BankingSystem.Services;

public class StatementService : IStatementService
{
    private readonly BankingDbContext _db;

    public StatementService(BankingDbContext db)
    {
        _db = db;
    }

    public async Task<StatementSummaryDto> GenerateStatementAsync(int accountId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var account = await _db.Accounts.FindAsync(accountId)
            ?? throw new KeyNotFoundException($"Account ID {accountId} not found.");

        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;

        // Calculate starting balance before the start date
        var priorCreditEntries = await _db.LedgerEntries
            .Where(e => e.AccountId == accountId && e.EntryType == EntryType.Credit && e.Timestamp < start)
            .Select(e => e.Amount)
            .ToListAsync();
        var priorCredits = priorCreditEntries.Sum();

        var priorDebitEntries = await _db.LedgerEntries
            .Where(e => e.AccountId == accountId && e.EntryType == EntryType.Debit && e.Timestamp < start)
            .Select(e => e.Amount)
            .ToListAsync();
        var priorDebits = priorDebitEntries.Sum();

        var startingBalance = priorCredits - priorDebits;

        // Fetch period entries
        var periodEntries = await _db.LedgerEntries
            .Include(e => e.Journal)
            .Where(e => e.AccountId == accountId && e.Timestamp >= start && e.Timestamp <= end)
            .OrderBy(e => e.Timestamp)
            .ToListAsync();

        var lines = new List<StatementLineDto>();
        decimal runningCalc = startingBalance;
        decimal totalDeposits = 0.00m;
        decimal totalWithdrawals = 0.00m;

        foreach (var entry in periodEntries)
        {
            var isCredit = entry.EntryType == EntryType.Credit;
            if (isCredit)
            {
                runningCalc += entry.Amount;
                totalDeposits += entry.Amount;
            }
            else
            {
                runningCalc -= entry.Amount;
                totalWithdrawals += entry.Amount;
            }

            lines.Add(new StatementLineDto(
                Date: entry.Timestamp,
                ReferenceNumber: entry.Journal?.ReferenceNumber ?? $"ENTRY-{entry.Id}",
                Description: entry.Journal?.Description ?? entry.Note ?? "Transaction Entry",
                TransactionType: entry.Journal?.TransactionType.ToString() ?? "JournalEntry",
                Direction: isCredit ? "Inflow (+)" : "Outflow (-)",
                Amount: entry.Amount,
                BalanceAfter: runningCalc
            ));
        }

        var endingBalance = startingBalance + totalDeposits - totalWithdrawals;

        return new StatementSummaryDto(
            AccountId: account.Id,
            AccountNumber: account.AccountNumber,
            AccountHolderName: account.AccountHolderName,
            AccountType: account.AccountType.ToString(),
            Currency: account.Currency,
            StartDate: start,
            EndDate: end,
            StartingBalance: startingBalance,
            TotalDeposits: totalDeposits,
            TotalWithdrawals: totalWithdrawals,
            EndingBalance: endingBalance,
            TransactionCount: lines.Count,
            Transactions: lines
        );
    }

    public async Task<string> GenerateHtmlStatementAsync(int accountId, DateTime? startDate = null, DateTime? endDate = null)
    {
        var stmt = await GenerateStatementAsync(accountId, startDate, endDate);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>Account Statement - {stmt.AccountNumber}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; margin: 30px; color: #1e293b; background: #fff; }");
        sb.AppendLine(".header { display: flex; justify-content: space-between; border-bottom: 2px solid #0f172a; padding-bottom: 20px; }");
        sb.AppendLine(".bank-title { font-size: 24px; font-weight: 800; color: #0f172a; letter-spacing: -0.5px; }");
        sb.AppendLine(".bank-subtitle { font-size: 13px; color: #64748b; margin-top: 4px; }");
        sb.AppendLine(".stmt-title { font-size: 18px; font-weight: 700; color: #3b82f6; text-align: right; }");
        sb.AppendLine(".meta-grid { display: grid; grid-template-columns: 1fr 1fr; margin-top: 24px; gap: 16px; background: #f8fafc; padding: 16px; border-radius: 8px; font-size: 14px; }");
        sb.AppendLine(".summary-cards { display: grid; grid-template-columns: repeat(4, 1fr); gap: 12px; margin: 24px 0; }");
        sb.AppendLine(".summary-card { background: #f1f5f9; padding: 12px; border-radius: 6px; text-align: center; }");
        sb.AppendLine(".summary-card .label { font-size: 12px; color: #64748b; text-transform: uppercase; }");
        sb.AppendLine(".summary-card .val { font-size: 18px; font-weight: 700; margin-top: 4px; }");
        sb.AppendLine(".val.green { color: #10b981; } .val.red { color: #ef4444; } .val.blue { color: #0284c7; }");
        sb.AppendLine("table { width: 100%; border-collapse: collapse; margin-top: 20px; font-size: 13px; }");
        sb.AppendLine("th { background: #0f172a; color: #fff; padding: 10px; text-align: left; font-weight: 600; }");
        sb.AppendLine("td { padding: 10px; border-bottom: 1px solid #e2e8f0; }");
        sb.AppendLine("tr:nth-child(even) { background: #f8fafc; }");
        sb.AppendLine(".badge { display: inline-block; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }");
        sb.AppendLine(".badge-in { background: #dcfce7; color: #166534; } .badge-out { background: #fee2e2; color: #991b1b; }");
        sb.AppendLine(".footer { margin-top: 40px; font-size: 11px; color: #94a3b8; border-top: 1px solid #e2e8f0; padding-top: 12px; text-align: center; }");
        sb.AppendLine("@media print { body { margin: 0; } .no-print { display: none; } }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine("  <div>");
        sb.AppendLine("    <div class=\"bank-title\">CANADA CORE BANKING SYSTEM</div>");
        sb.AppendLine("    <div class=\"bank-subtitle\">Double-Entry Financial Ledger & Settlement Engine</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div style=\"text-align: right;\">");
        sb.AppendLine("    <div class=\"stmt-title\">OFFICIAL ACCOUNT STATEMENT</div>");
        sb.AppendLine($"    <div class=\"bank-subtitle\">Period: {stmt.StartDate:yyyy-MM-dd} to {stmt.EndDate:yyyy-MM-dd}</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"meta-grid\">");
        sb.AppendLine("  <div>");
        sb.AppendLine($"    <div><strong>Account Holder:</strong> {stmt.AccountHolderName}</div>");
        sb.AppendLine($"    <div><strong>Account Number:</strong> {stmt.AccountNumber}</div>");
        sb.AppendLine($"    <div><strong>Account Type:</strong> {stmt.AccountType}</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div>");
        sb.AppendLine($"    <div><strong>Routing Transit:</strong> 021000021</div>");
        sb.AppendLine($"    <div><strong>Currency:</strong> {stmt.Currency} (Simulated)</div>");
        sb.AppendLine($"    <div><strong>Generated On:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<div class=\"summary-cards\">");
        sb.AppendLine($"  <div class=\"summary-card\"><div class=\"label\">Starting Balance</div><div class=\"val\">${stmt.StartingBalance:N2}</div></div>");
        sb.AppendLine($"  <div class=\"summary-card\"><div class=\"label\">Total Deposits</div><div class=\"val green\">+${stmt.TotalDeposits:N2}</div></div>");
        sb.AppendLine($"  <div class=\"summary-card\"><div class=\"label\">Total Outflows</div><div class=\"val red\">-${stmt.TotalWithdrawals:N2}</div></div>");
        sb.AppendLine($"  <div class=\"summary-card\"><div class=\"label\">Ending Balance</div><div class=\"val blue\">${stmt.EndingBalance:N2}</div></div>");
        sb.AppendLine("</div>");

        sb.AppendLine("<table>");
        sb.AppendLine("  <thead><tr><th>Date & Time</th><th>Reference</th><th>Description</th><th>Type</th><th>Flow</th><th style=\"text-align:right;\">Amount</th><th style=\"text-align:right;\">Balance After</th></tr></thead>");
        sb.AppendLine("  <tbody>");

        if (stmt.Transactions.Count == 0)
        {
            sb.AppendLine("    <tr><td colspan=\"7\" style=\"text-align: center; color: #64748b; padding: 20px;\">No transactions recorded during this statement period.</td></tr>");
        }
        else
        {
            foreach (var t in stmt.Transactions)
            {
                var isCredit = t.Direction.Contains("Inflow");
                var badgeClass = isCredit ? "badge-in" : "badge-out";
                sb.AppendLine("    <tr>");
                sb.AppendLine($"      <td>{t.Date:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine($"      <td><code>{t.ReferenceNumber}</code></td>");
                sb.AppendLine($"      <td>{t.Description}</td>");
                sb.AppendLine($"      <td>{t.TransactionType}</td>");
                sb.AppendLine($"      <td><span class=\"badge {badgeClass}\">{t.Direction}</span></td>");
                sb.AppendLine($"      <td style=\"text-align:right; font-weight:600; color: {(isCredit ? "#166534" : "#991b1b")};\">{(isCredit ? "+" : "-")}${t.Amount:N2}</td>");
                sb.AppendLine($"      <td style=\"text-align:right; font-weight:700;\">${t.BalanceAfter:N2}</td>");
                sb.AppendLine("    </tr>");
            }
        }

        sb.AppendLine("  </tbody>");
        sb.AppendLine("</table>");

        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine("  This document is an electronic statement generated by Canada Core Banking System double-entry ledger. All balances represent simulated funds for simulation and testing purposes only.");
        sb.AppendLine("</div>");

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
