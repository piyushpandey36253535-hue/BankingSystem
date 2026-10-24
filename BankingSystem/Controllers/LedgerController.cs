using Microsoft.AspNetCore.Mvc;
using BankingSystem.Models;
using BankingSystem.Services;

namespace BankingSystem.Controllers;

public record ReverseJournalRequest(string Reason, string ReversedBy = "Compliance Auditor");

[ApiController]
[Route("api/[controller]")]
public class LedgerController : ControllerBase
{
    private readonly ILedgerService _ledgerService;

    public LedgerController(ILedgerService ledgerService)
    {
        _ledgerService = ledgerService;
    }

    [HttpGet("journals")]
    public async Task<ActionResult<List<LedgerJournalDto>>> GetJournals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] int? accountId = null)
    {
        var journals = await _ledgerService.GetJournalsAsync(page, pageSize, accountId);
        var dtos = journals.Select(j => new LedgerJournalDto(
            j.Id,
            j.JournalNumber,
            j.ReferenceNumber,
            j.TransactionType.ToString(),
            j.Status.ToString(),
            j.Amount,
            j.Description,
            j.SourceAccountId,
            j.SourceAccount?.AccountNumber,
            j.SourceAccount?.AccountHolderName,
            j.DestinationAccountId,
            j.DestinationAccount?.AccountNumber,
            j.DestinationAccount?.AccountHolderName,
            j.ExternalInstitutionName,
            j.Timestamp,
            j.Entries.Select(e => new LedgerEntryDto(
                e.Id,
                e.LedgerJournalId,
                e.AccountId,
                e.Account?.AccountNumber ?? "Unknown",
                e.Account?.AccountHolderName ?? "Unknown",
                e.EntryType.ToString(),
                e.Amount,
                e.RunningBalanceAfter,
                e.Timestamp,
                e.Note
            )).ToList(),
            j.FraudAlert?.Id,
            j.FraudAlert?.RiskScore
        )).ToList();

        return Ok(dtos);
    }

    [HttpGet("journals/{id:int}")]
    public async Task<ActionResult<LedgerJournalDto>> GetJournalById(int id)
    {
        var j = await _ledgerService.GetJournalByIdAsync(id);
        if (j == null) return NotFound(new { message = $"Journal #{id} not found." });

        return Ok(new LedgerJournalDto(
            j.Id,
            j.JournalNumber,
            j.ReferenceNumber,
            j.TransactionType.ToString(),
            j.Status.ToString(),
            j.Amount,
            j.Description,
            j.SourceAccountId,
            j.SourceAccount?.AccountNumber,
            j.SourceAccount?.AccountHolderName,
            j.DestinationAccountId,
            j.DestinationAccount?.AccountNumber,
            j.DestinationAccount?.AccountHolderName,
            j.ExternalInstitutionName,
            j.Timestamp,
            j.Entries.Select(e => new LedgerEntryDto(
                e.Id,
                e.LedgerJournalId,
                e.AccountId,
                e.Account?.AccountNumber ?? "Unknown",
                e.Account?.AccountHolderName ?? "Unknown",
                e.EntryType.ToString(),
                e.Amount,
                e.RunningBalanceAfter,
                e.Timestamp,
                e.Note
            )).ToList(),
            j.FraudAlert?.Id,
            j.FraudAlert?.RiskScore
        ));
    }

    [HttpPost("journals/{id:int}/reverse")]
    public async Task<ActionResult> ReverseJournal(int id, [FromBody] ReverseJournalRequest request)
    {
        try
        {
            var reversal = await _ledgerService.ReverseJournalAsync(id, request.Reason, request.ReversedBy);
            return Ok(new
            {
                message = "Journal successfully reversed with opposite double-entry lines.",
                reversalJournalNumber = reversal.JournalNumber,
                originalJournalId = id,
                amount = reversal.Amount,
                timestamp = reversal.Timestamp
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("trial-balance")]
    public async Task<ActionResult> GetTrialBalance()
    {
        var (debits, credits, isBalanced) = await _ledgerService.VerifyTrialBalanceAsync();
        return Ok(new
        {
            totalDebits = debits,
            totalCredits = credits,
            variance = Math.Abs(debits - credits),
            isBalanced,
            verificationStatus = isBalanced ? "PERFECTLY_BALANCED" : "IMBALANCE_DETECTED",
            checkedAt = DateTime.UtcNow
        });
    }
}
