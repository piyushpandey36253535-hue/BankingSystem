using Microsoft.EntityFrameworkCore;
using BankingSystem.Data;
using BankingSystem.Models;

namespace BankingSystem.Services;

public class ScheduledTransferProcessorHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScheduledTransferProcessorHostedService> _logger;

    public ScheduledTransferProcessorHostedService(
        IServiceProvider serviceProvider,
        ILogger<ScheduledTransferProcessorHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ScheduledTransferProcessor Hosted Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueTransfersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing scheduled transfers in background worker.");
            }

            // Check every 20 seconds
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }

        _logger.LogInformation("ScheduledTransferProcessor Hosted Service stopped.");
    }

    private async Task ProcessDueTransfersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankingDbContext>();
        var transferService = scope.ServiceProvider.GetRequiredService<ITransferService>();

        var now = DateTime.UtcNow;
        var dueTransfers = await db.ScheduledTransfers
            .Where(s => s.IsActive && s.NextExecutionDate <= now)
            .ToListAsync(cancellationToken);

        if (dueTransfers.Count == 0) return;

        _logger.LogInformation("Found {Count} due scheduled transfers to process.", dueTransfers.Count);

        foreach (var scheduled in dueTransfers)
        {
            try
            {
                var result = await transferService.ExecuteTransferAsync(new InitiateTransferRequest(
                    SourceAccountId: scheduled.SourceAccountId,
                    DestinationAccountId: scheduled.DestinationAccountId,
                    ExternalAccountNumber: null,
                    ExternalRoutingOrSwift: null,
                    ExternalInstitutionName: null,
                    Amount: scheduled.Amount,
                    Description: $"[SCHEDULED-{scheduled.Frequency}] {scheduled.Description}",
                    IsInterBank: false
                ));

                scheduled.LastExecutionDate = DateTime.UtcNow;

                // Advance schedule based on frequency
                scheduled.NextExecutionDate = scheduled.Frequency switch
                {
                    ScheduleFrequency.Daily => scheduled.NextExecutionDate.AddDays(1),
                    ScheduleFrequency.Weekly => scheduled.NextExecutionDate.AddDays(7),
                    ScheduleFrequency.Monthly => scheduled.NextExecutionDate.AddMonths(1),
                    _ => scheduled.NextExecutionDate // Once
                };

                if (scheduled.Frequency == ScheduleFrequency.Once)
                {
                    scheduled.IsActive = false;
                }

                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Processed scheduled transfer #{Id}: Result={Success}, Message={Msg}",
                    scheduled.Id, result.Success, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute scheduled transfer #{Id}: {Message}", scheduled.Id, ex.Message);
            }
        }
    }
}
