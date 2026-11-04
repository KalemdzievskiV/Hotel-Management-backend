using System.Threading.Channels;
using HotelManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Services.Push;

/// <summary>One push to one browser. Url is the page to open when it's tapped.</summary>
public record WebPushMessage(string Endpoint, string P256dh, string Auth, string Title, string Body, string Url, string Tag);

/// <summary>Browser pushes waiting to be sent; requests only enqueue (see <see cref="IPushQueue"/>)</summary>
public interface IWebPushQueue
{
    void Enqueue(WebPushMessage message);
}

public class WebPushQueue : IWebPushQueue
{
    private readonly Channel<WebPushMessage> _channel = Channel.CreateBounded<WebPushMessage>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<WebPushMessage> Reader => _channel.Reader;

    public void Enqueue(WebPushMessage message) => _channel.Writer.TryWrite(message);
}

/// <summary>
/// Sends queued browser pushes and forgets subscriptions the push service says are gone, e.g.
/// the user turned notifications off or uninstalled the web app.
/// </summary>
public class WebPushDispatcher : BackgroundService
{
    private readonly WebPushQueue _queue;
    private readonly IWebPushSender _sender;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<WebPushDispatcher> _logger;

    public WebPushDispatcher(
        WebPushQueue queue,
        IWebPushSender sender,
        IServiceScopeFactory scopes,
        ILogger<WebPushDispatcher> logger)
    {
        _queue = queue;
        _sender = sender;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            var result = await _sender.SendAsync(message, stoppingToken);
            if (result.Status == WebPushSendStatus.Gone)
                await ForgetAsync(message.Endpoint, stoppingToken);
            else if (result.Status == WebPushSendStatus.Failed)
                // The notification is in the in-app list either way
                _logger.LogWarning("Sending a browser push failed: {Error}", result.Error);
        }
    }
    private async Task ForgetAsync(string endpoint, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.WebPushSubscriptions.Where(s => s.Endpoint == endpoint).ExecuteDeleteAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Removing an expired browser push subscription failed");
        }
    }
}
