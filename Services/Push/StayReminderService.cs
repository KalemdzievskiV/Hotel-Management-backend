using HotelManagement.Services.Interfaces;

namespace HotelManagement.Services.Push;

/// <summary>
/// Every minute, sends the hotel's managers a reminder for each check-in and check-out due
/// within 30 minutes (see <see cref="INotificationService.SendStayRemindersAsync"/>).
/// </summary>
public class StayReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<StayReminderService> _logger;

    public StayReminderService(IServiceScopeFactory scopes, ILogger<StayReminderService> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var sent = await scope.ServiceProvider.GetRequiredService<INotificationService>().SendStayRemindersAsync();
                if (sent > 0)
                    _logger.LogInformation("Sent {Count} check-in/check-out reminders", sent);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Try again next minute rather than stopping the job
                _logger.LogError(ex, "Sending check-in/check-out reminders failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
