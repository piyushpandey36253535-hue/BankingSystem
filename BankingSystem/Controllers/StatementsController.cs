using Microsoft.AspNetCore.Mvc;
using BankingSystem.Models;
using BankingSystem.Services;

namespace BankingSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatementsController : ControllerBase
{
    private readonly IStatementService _statementService;

    public StatementsController(IStatementService statementService)
    {
        _statementService = statementService;
    }

    [HttpGet("{accountId:int}")]
    public async Task<ActionResult<StatementSummaryDto>> GetStatement(
        int accountId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var stmt = await _statementService.GenerateStatementAsync(accountId, startDate, endDate);
            return Ok(stmt);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{accountId:int}/html")]
    public async Task<IActionResult> GetHtmlStatement(
        int accountId,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate)
    {
        try
        {
            var html = await _statementService.GenerateHtmlStatementAsync(accountId, startDate, endDate);
            return Content(html, "text/html");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
