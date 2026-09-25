using HotelManagement.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace HotelManagement.Services.Billing;

/// <summary>
/// Runs BillingService.RunMaintenanceAsync on a schedule: charges renewals that are due (for the
/// fake provider) and moves trials and unpaid subscriptions that have run out to the Free plan.
/// </summary>
public class SubscriptionMaintenanceService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly BillingOptions _options;
    private readonly ILogger<SubscriptionMaintenanceService> _logger;

    public SubscriptionMaintenanceService(IServiceScopeFactory scopes, IOptions<BillingOptions> options, ILogger<SubscriptionMaintenanceService> logger)
    {
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.RunMaintenance)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, _options.MaintenanceIntervalMinutes)));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var changed = await scope.ServiceProvider.GetRequiredService<IBillingService>().RunMaintenanceAsync();
                if (changed > 0)
                    _logger.LogInformation("Subscription maintenance updated {Count} subscriptions", changed);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Try again next time rather than stopping the job
                _logger.LogError(ex, "Subscription maintenance failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
