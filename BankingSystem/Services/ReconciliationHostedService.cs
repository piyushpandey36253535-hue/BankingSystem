using BankingSystem.Services;

namespace BankingSystem.Services;

public class ReconciliationHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ReconciliationHostedService> _logger;

    public ReconciliationHostedService(
        IServiceProvider serviceProvider,
        ILogger<ReconciliationHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reconciliation Hosted Service background worker started.");

        // Initial delay on startup to allow database initialization to complete
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reconService = scope.ServiceProvider.GetRequiredService<IReconciliationService>();
                var result = await reconService.RunReconciliationAuditAsync();

                _logger.LogInformation("Background Reconciliation run completed: Status={Status}, Balanced={IsBalanced}, Accounts={Accounts}",
                    result.Status, result.IsLedgerBalanced, result.TotalAccountsAudited);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing periodic background reconciliation audit.");
            }

            // Run reconciliation audit every 3 minutes
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
        }

        _logger.LogInformation("Reconciliation Hosted Service background worker stopped.");
    }
}
