using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;

namespace BankingSystem.Services;

public class TransferService : ITransferService
{
    private readonly BankingDbContext _db;
    private readonly ILedgerService _ledgerService;
    private readonly IFraudDetectionService _fraudService;
    private readonly ILogger<TransferService> _logger;

    public TransferService(
        BankingDbContext db,
        ILedgerService ledgerService,
        IFraudDetectionService fraudService,
        ILogger<TransferService> logger)
    {
        _db = db;
        _ledgerService = ledgerService;
        _fraudService = fraudService;
        _logger = logger;
    }

    public async Task<TransferExecutionResult> ExecuteTransferAsync(InitiateTransferRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Transfer amount must be strictly greater than zero.", nameof(request.Amount));
        }

        var source = await _db.Accounts.FindAsync(request.SourceAccountId)
            ?? throw new KeyNotFoundException($"Source account ID {request.SourceAccountId} not found.");

        if (source.Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"Cannot initiate transfer: Source account {source.AccountNumber} is {source.Status}.");
        }

        if (source.AvailableBalance < request.Amount)
        {
            throw new InvalidOperationException($"Insufficient available balance. Available: ${source.AvailableBalance:N2}, Requested: ${request.Amount:N2}");
        }

        Account? destination = null;
        if (!request.IsInterBank)
        {
            if (!request.DestinationAccountId.HasValue)
            {
                throw new ArgumentException("Destination account is required for intra-bank transfer.", nameof(request.DestinationAccountId));
            }

            if (request.SourceAccountId == request.DestinationAccountId.Value)
            {
                throw new InvalidOperationException("Source and destination accounts cannot be identical.");
            }

            destination = await _db.Accounts.FindAsync(request.DestinationAccountId.Value)
                ?? throw new KeyNotFoundException($"Destination account ID {request.DestinationAccountId} not found.");

            if (destination.Status == AccountStatus.Closed)
            {
                throw new InvalidOperationException($"Cannot transfer to closed account {destination.AccountNumber}.");
            }
        }

        // Run Fraud & Velocity Evaluation
        var fraudEvaluation = await _fraudService.EvaluateTransferAsync(
            source,
            request.Amount,
            request.ExternalInstitutionName,
            request.IsInterBank
        );

        var referenceNumber = $"TXN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";

        // If transfer is blocked or requires holding by fraud engine
        if (!fraudEvaluation.IsAllowed)
        {
            using var dbTransaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var journalNumber = $"JRN-HOLD-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";
                var heldJournal = new LedgerJournal
                {
                    JournalNumber = journalNumber,
                    ReferenceNumber = referenceNumber,
                    TransactionType = request.IsInterBank ? TransactionType.InterBankTransfer : TransactionType.IntraBankTransfer,
                    Status = JournalStatus.Held,
                    Amount = request.Amount,
                    Description = $"{request.Description} [HELD BY FRAUD DETECTION]",
                    SourceAccountId = source.Id,
                    DestinationAccountId = destination?.Id,
                    ExternalInstitutionName = request.ExternalInstitutionName,
                    ExternalRoutingOrSwift = request.ExternalRoutingOrSwift,
                    ExternalAccountNumber = request.ExternalAccountNumber,
                    Timestamp = DateTime.UtcNow
                };

                _db.LedgerJournals.Add(heldJournal);

                // Place funds on hold (reduce available balance)
                source.AvailableBalance -= request.Amount;
                source.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                var alert = await _fraudService.CreateAlertAsync(source.Id, heldJournal.Id, request.Amount, fraudEvaluation);

                await dbTransaction.CommitAsync();

                _logger.LogWarning("Transfer of ${Amount:N2} placed on HOLD due to fraud risk score {Score}. Alert #{AlertId} created.",
                    request.Amount, fraudEvaluation.RiskScore, alert.Id);

                return new TransferExecutionResult(
                    Success: false,
                    Message: $"Transfer placed on HOLD for Compliance Review. Risk score: {fraudEvaluation.RiskScore}/100. Violations: {string.Join(", ", fraudEvaluation.TriggeredRules)}",
                    JournalId: heldJournal.Id,
                    ReferenceNumber: referenceNumber,
                    HeldForFraudReview: true,
                    FraudAlertId: alert.Id,
                    RiskScore: fraudEvaluation.RiskScore,
                    Amount: request.Amount
                );
            }
            catch
            {
                await dbTransaction.RollbackAsync();
                throw;
            }
        }

        // Clean / Approved transfer: Execute double-entry ledger posting immediately
        var txnType = request.IsInterBank ? TransactionType.InterBankTransfer : TransactionType.IntraBankTransfer;
        var journal = await _ledgerService.PostJournalAsync(
            transactionType: txnType,
            amount: request.Amount,
            description: request.Description,
            referenceNumber: referenceNumber,
            sourceAccountId: source.Id,
            destinationAccountId: destination?.Id,
            externalInstitution: request.ExternalInstitutionName,
            externalRouting: request.ExternalRoutingOrSwift,
            externalAccount: request.ExternalAccountNumber
        );

        // If soft review is recommended, log informational alert
        int? alertId = null;
        if (fraudEvaluation.RequiresReview)
        {
            var alert = await _fraudService.CreateAlertAsync(source.Id, journal.Id, request.Amount, fraudEvaluation);
            alertId = alert.Id;
        }

        return new TransferExecutionResult(
            Success: true,
            Message: "Transfer completed and posted to general ledger successfully.",
            JournalId: journal.Id,
            ReferenceNumber: referenceNumber,
            HeldForFraudReview: false,
            FraudAlertId: alertId,
            RiskScore: fraudEvaluation.RiskScore,
            Amount: request.Amount
        );
    }

    public async Task<LedgerJournal> SimulateFaucetDepositAsync(SimulateFaucetDepositRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Deposit amount must be strictly greater than zero.", nameof(request.Amount));
        }

        var destination = await _db.Accounts.FindAsync(request.AccountId)
            ?? throw new KeyNotFoundException($"Account ID {request.AccountId} not found.");

        var refNumber = $"SIM-DEP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        var journal = await _ledgerService.PostJournalAsync(
            transactionType: TransactionType.Deposit,
            amount: request.Amount,
            description: $"{request.Note} ({destination.AccountNumber})",
            referenceNumber: refNumber,
            sourceAccountId: null,
            destinationAccountId: destination.Id,
            isDeposit: true
        );

        _logger.LogInformation("Simulated faucet deposit of ${Amount:N2} credited to {AccountNumber}", request.Amount, destination.AccountNumber);
        return journal;
    }

    public async Task<ScheduledTransfer> CreateScheduledTransferAsync(CreateScheduledTransferRequest request)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.", nameof(request.Amount));
        }

        var source = await _db.Accounts.FindAsync(request.SourceAccountId)
            ?? throw new KeyNotFoundException($"Source account {request.SourceAccountId} not found.");
        var dest = await _db.Accounts.FindAsync(request.DestinationAccountId)
            ?? throw new KeyNotFoundException($"Destination account {request.DestinationAccountId} not found.");

        var schedule = new ScheduledTransfer
        {
            SourceAccountId = request.SourceAccountId,
            DestinationAccountId = request.DestinationAccountId,
            Amount = request.Amount,
            Description = request.Description,
            Frequency = request.Frequency,
            NextExecutionDate = request.StartDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.ScheduledTransfers.Add(schedule);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Scheduled transfer #{Id} created from {Source} to {Dest} for ${Amount:N2}",
            schedule.Id, source.AccountNumber, dest.AccountNumber, request.Amount);

        return schedule;
    }

    public async Task<List<ScheduledTransfer>> GetScheduledTransfersAsync(int? accountId = null)
    {
        var query = _db.ScheduledTransfers
            .Include(s => s.SourceAccount)
            .Include(s => s.DestinationAccount)
            .AsNoTracking();

        if (accountId.HasValue)
        {
            query = query.Where(s => s.SourceAccountId == accountId || s.DestinationAccountId == accountId);
        }

        return await query.OrderByDescending(s => s.CreatedAt).ToListAsync();
    }

    public async Task<bool> CancelScheduledTransferAsync(int scheduledTransferId)
    {
        var schedule = await _db.ScheduledTransfers.FindAsync(scheduledTransferId);
        if (schedule == null) return false;

        schedule.IsActive = false;
        await _db.SaveChangesAsync();
        return true;
    }
}
