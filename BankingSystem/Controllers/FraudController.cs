using Microsoft.AspNetCore.Mvc;
using BankingSystem.Models;
using BankingSystem.Services;

namespace BankingSystem.Controllers;

public record SimulateAttackRequest(int AccountId, string ScenarioType);

[ApiController]
[Route("api/[controller]")]
public class FraudController : ControllerBase
{
    private readonly IFraudDetectionService _fraudService;

    public FraudController(IFraudDetectionService fraudService)
    {
        _fraudService = fraudService;
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<List<FraudAlertDto>>> GetAlerts(
        [FromQuery] string? status = null,
        [FromQuery] int? accountId = null)
    {
        var alerts = await _fraudService.GetAlertsAsync(status, accountId);
        var dtos = alerts.Select(a => new FraudAlertDto(
            a.Id,
            a.LedgerJournalId,
            a.Journal?.ReferenceNumber,
            a.AccountId,
            a.Account.AccountNumber,
            a.Account.AccountHolderName,
            a.RiskScore,
            a.Severity.ToString(),
            a.Status.ToString(),
            a.TriggeredRules,
            a.Details,
            a.FlaggedAmount,
            a.ReviewNotes,
            a.ReviewedBy,
            a.CreatedAt,
            a.ResolvedAt
        )).ToList();

        return Ok(dtos);
    }

    [HttpGet("alerts/{id:int}")]
    public async Task<ActionResult<FraudAlertDto>> GetAlertById(int id)
    {
        var a = await _fraudService.GetAlertByIdAsync(id);
        if (a == null) return NotFound(new { message = $"Fraud Alert #{id} not found." });

        return Ok(new FraudAlertDto(
            a.Id,
            a.LedgerJournalId,
            a.Journal?.ReferenceNumber,
            a.AccountId,
            a.Account.AccountNumber,
            a.Account.AccountHolderName,
            a.RiskScore,
            a.Severity.ToString(),
            a.Status.ToString(),
            a.TriggeredRules,
            a.Details,
            a.FlaggedAmount,
            a.ReviewNotes,
            a.ReviewedBy,
            a.CreatedAt,
            a.ResolvedAt
        ));
    }

    [HttpPost("alerts/{id:int}/review")]
    public async Task<ActionResult> ReviewAlert(int id, [FromBody] ReviewFraudAlertRequest request)
    {
        try
        {
            var alert = await _fraudService.ReviewAlertAsync(id, request);
            return Ok(new
            {
                message = $"Alert #{id} successfully resolved as {alert.Status}.",
                alertId = alert.Id,
                status = alert.Status.ToString(),
                resolvedAt = alert.ResolvedAt
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("simulate-attack")]
    public async Task<ActionResult> SimulateAttack([FromBody] SimulateAttackRequest request)
    {
        try
        {
            var alerts = await _fraudService.SimulateAttackScenarioAsync(request.AccountId, request.ScenarioType);
            return Ok(new
            {
                message = $"Simulated {request.ScenarioType} attack executed.",
                alertsGenerated = alerts.Count,
                alertIds = alerts.Select(a => a.Id).ToList()
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
