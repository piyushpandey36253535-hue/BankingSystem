using Microsoft.AspNetCore.Mvc;
using BankingSystem.Models;
using BankingSystem.Services;

namespace BankingSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransfersController : ControllerBase
{
    private readonly ITransferService _transferService;

    public TransfersController(ITransferService transferService)
    {
        _transferService = transferService;
    }

    [HttpPost]
    public async Task<ActionResult<TransferExecutionResult>> InitiateTransfer([FromBody] InitiateTransferRequest request)
    {
        try
        {
            var result = await _transferService.ExecuteTransferAsync(request);
            if (result.HeldForFraudReview)
            {
                // Return 202 Accepted with details on hold
                return Accepted(result);
            }
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("faucet")]
    public async Task<ActionResult> SimulateFaucetDeposit([FromBody] SimulateFaucetDepositRequest request)
    {
        try
        {
            var journal = await _transferService.SimulateFaucetDepositAsync(request);
            return Ok(new
            {
                message = $"Successfully funded account with simulated cash ${request.Amount:N2}",
                journalNumber = journal.JournalNumber,
                referenceNumber = journal.ReferenceNumber,
                amount = journal.Amount,
                timestamp = journal.Timestamp
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("scheduled")]
    public async Task<ActionResult<List<ScheduledTransfer>>> GetScheduled([FromQuery] int? accountId)
    {
        var list = await _transferService.GetScheduledTransfersAsync(accountId);
        return Ok(list);
    }

    [HttpPost("scheduled")]
    public async Task<ActionResult<ScheduledTransfer>> CreateScheduled([FromBody] CreateScheduledTransferRequest request)
    {
        try
        {
            var result = await _transferService.CreateScheduledTransferAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("scheduled/{id:int}")]
    public async Task<ActionResult> CancelScheduled(int id)
    {
        var success = await _transferService.CancelScheduledTransferAsync(id);
        if (!success) return NotFound(new { message = $"Scheduled transfer #{id} not found." });
        return Ok(new { message = "Scheduled transfer canceled successfully." });
    }
}
