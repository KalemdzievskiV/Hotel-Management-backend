using HotelManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HotelManagement.Services.Push;

/// <summary>
/// Sends queued pushes in batches and forgets device tokens that no longer work.
/// </summary>
public class PushDispatcher : BackgroundService
{
    private readonly PushQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly PushOptions _options;
    private readonly ILogger<PushDispatcher> _logger;

    public PushDispatcher(PushQueue queue, IServiceScopeFactory scopes, IOptions<PushOptions> options, ILogger<PushDispatcher> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _queue.Reader;
        while (await reader.WaitToReadAsync(stoppingToken))
        {
            var batch = new List<PushMessage>();
            while (batch.Count < ExpoPushClient.MaxBatchSize && reader.TryRead(out var message))
                batch.Add(message);

            if (!_options.Enabled)
                continue;

            try
            {
                using var scope = _scopes.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<IExpoPushClient>();
                var gone = await client.SendAsync(batch, stoppingToken);
                if (gone.Count > 0)
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var stale = await db.DeviceTokens.Where(d => gone.Contains(d.Token)).ToListAsync(stoppingToken);
                    db.DeviceTokens.RemoveRange(stale);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // A push is a nice-to-have: the notification is in the app's list either way
                _logger.LogWarning(ex, "Sending {Count} push notifications failed", batch.Count);
            }
        }
    }
}
