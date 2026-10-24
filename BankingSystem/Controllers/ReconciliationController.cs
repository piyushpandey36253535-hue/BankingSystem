using Microsoft.AspNetCore.Mvc;
using BankingSystem.Models;
using BankingSystem.Services;

namespace BankingSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReconciliationController : ControllerBase
{
    private readonly IReconciliationService _reconciliationService;

    public ReconciliationController(IReconciliationService reconciliationService)
    {
        _reconciliationService = reconciliationService;
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<ReconciliationSummaryDto>>> GetHistory([FromQuery] int count = 20)
    {
        var records = await _reconciliationService.GetReconciliationHistoryAsync(count);
        var dtos = records.Select(r => new ReconciliationSummaryDto(
            r.Id,
            r.AuditTimestamp,
            r.TotalAccountsAudited,
            r.TotalLedgerDebits,
            r.TotalLedgerCredits,
            r.IsLedgerBalanced,
            r.TotalAccountBalancesSum,
            r.TotalDiscrepancyAmount,
            r.DiscrepancyCount,
            r.Status.ToString(),
            r.SummaryMessage,
            r.DiscrepancyDetailsJson
        )).ToList();

        return Ok(dtos);
    }

    [HttpGet("latest")]
    public async Task<ActionResult<ReconciliationSummaryDto>> GetLatest()
    {
        var r = await _reconciliationService.GetLatestReconciliationAsync();
        if (r == null) return NotFound(new { message = "No reconciliation records found." });

        return Ok(new ReconciliationSummaryDto(
            r.Id,
            r.AuditTimestamp,
            r.TotalAccountsAudited,
            r.TotalLedgerDebits,
            r.TotalLedgerCredits,
            r.IsLedgerBalanced,
            r.TotalAccountBalancesSum,
            r.TotalDiscrepancyAmount,
            r.DiscrepancyCount,
            r.Status.ToString(),
            r.SummaryMessage,
            r.DiscrepancyDetailsJson
        ));
    }

    [HttpPost("run")]
    public async Task<ActionResult<ReconciliationSummaryDto>> RunAuditNow()
    {
        var r = await _reconciliationService.RunReconciliationAuditAsync();
        return Ok(new ReconciliationSummaryDto(
            r.Id,
            r.AuditTimestamp,
            r.TotalAccountsAudited,
            r.TotalLedgerDebits,
            r.TotalLedgerCredits,
            r.IsLedgerBalanced,
            r.TotalAccountBalancesSum,
            r.TotalDiscrepancyAmount,
            r.DiscrepancyCount,
            r.Status.ToString(),
            r.SummaryMessage,
            r.DiscrepancyDetailsJson
        ));
    }
}
