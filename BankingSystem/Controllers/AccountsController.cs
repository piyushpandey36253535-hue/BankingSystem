using Microsoft.AspNetCore.Mvc;
using BankingSystem.Models;
using BankingSystem.Services;

namespace BankingSystem.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountsController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AccountDto>>> GetAll([FromQuery] bool includeInternal = false)
    {
        var accounts = await _accountService.GetAllAccountsAsync(includeInternal);
        var dtos = accounts.Select(a => new AccountDto(
            a.Id,
            a.AccountNumber,
            a.RoutingNumber,
            a.AccountHolderName,
            a.Email,
            a.Phone,
            a.AccountType.ToString(),
            a.Status.ToString(),
            a.KycStatus.ToString(),
            a.KycDocumentType,
            a.KycDocumentNumber,
            a.Currency,
            a.Balance,
            a.AvailableBalance,
            a.DailyTransferLimit,
            a.SingleTransferLimit,
            a.CreatedAt
        )).ToList();

        return Ok(dtos);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<AccountDto>> GetById(int id)
    {
        var a = await _accountService.GetAccountByIdAsync(id);
        if (a == null) return NotFound(new { message = $"Account #{id} not found." });

        return Ok(new AccountDto(
            a.Id,
            a.AccountNumber,
            a.RoutingNumber,
            a.AccountHolderName,
            a.Email,
            a.Phone,
            a.AccountType.ToString(),
            a.Status.ToString(),
            a.KycStatus.ToString(),
            a.KycDocumentType,
            a.KycDocumentNumber,
            a.Currency,
            a.Balance,
            a.AvailableBalance,
            a.DailyTransferLimit,
            a.SingleTransferLimit,
            a.CreatedAt
        ));
    }

    [HttpPost]
    public async Task<ActionResult<AccountDto>> Create([FromBody] CreateAccountRequest request)
    {
        try
        {
            var a = await _accountService.CreateAccountAsync(request);
            var dto = new AccountDto(
                a.Id,
                a.AccountNumber,
                a.RoutingNumber,
                a.AccountHolderName,
                a.Email,
                a.Phone,
                a.AccountType.ToString(),
                a.Status.ToString(),
                a.KycStatus.ToString(),
                a.KycDocumentType,
                a.KycDocumentNumber,
                a.Currency,
                a.Balance,
                a.AvailableBalance,
                a.DailyTransferLimit,
                a.SingleTransferLimit,
                a.CreatedAt
            );
            return CreatedAtAction(nameof(GetById), new { id = a.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id:int}/verify-kyc")]
    public async Task<ActionResult<AccountDto>> VerifyKyc(int id, [FromBody] VerifyKycRequest request)
    {
        try
        {
            var a = await _accountService.VerifyKycAsync(id, request);
            return Ok(new AccountDto(
                a.Id,
                a.AccountNumber,
                a.RoutingNumber,
                a.AccountHolderName,
                a.Email,
                a.Phone,
                a.AccountType.ToString(),
                a.Status.ToString(),
                a.KycStatus.ToString(),
                a.KycDocumentType,
                a.KycDocumentNumber,
                a.Currency,
                a.Balance,
                a.AvailableBalance,
                a.DailyTransferLimit,
                a.SingleTransferLimit,
                a.CreatedAt
            ));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/status")]
    public async Task<ActionResult<AccountDto>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        try
        {
            var a = await _accountService.UpdateStatusAsync(id, request.Status, request.Reason);
            return Ok(new AccountDto(
                a.Id,
                a.AccountNumber,
                a.RoutingNumber,
                a.AccountHolderName,
                a.Email,
                a.Phone,
                a.AccountType.ToString(),
                a.Status.ToString(),
                a.KycStatus.ToString(),
                a.KycDocumentType,
                a.KycDocumentNumber,
                a.Currency,
                a.Balance,
                a.AvailableBalance,
                a.DailyTransferLimit,
                a.SingleTransferLimit,
                a.CreatedAt
            ));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut("{id:int}/limits")]
    public async Task<ActionResult<AccountDto>> UpdateLimits(int id, [FromBody] UpdateLimitsRequest request)
    {
        try
        {
            var a = await _accountService.UpdateLimitsAsync(id, request.DailyLimit, request.SingleLimit);
            return Ok(new AccountDto(
                a.Id,
                a.AccountNumber,
                a.RoutingNumber,
                a.AccountHolderName,
                a.Email,
                a.Phone,
                a.AccountType.ToString(),
                a.Status.ToString(),
                a.KycStatus.ToString(),
                a.KycDocumentType,
                a.KycDocumentNumber,
                a.Currency,
                a.Balance,
                a.AvailableBalance,
                a.DailyTransferLimit,
                a.SingleTransferLimit,
                a.CreatedAt
            ));
        }
        catch (Exception ex) when (ex is KeyNotFoundException or ArgumentException)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
