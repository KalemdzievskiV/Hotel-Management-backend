using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using HotelManagement.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebPush;

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
/// Sends queued browser pushes (signed with the VAPID keys) and forgets subscriptions the push
/// service says are gone, e.g. the user turned notifications off or uninstalled the web app.
/// </summary>
public class WebPushDispatcher : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly WebPushQueue _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly IHttpClientFactory _httpClients;
    private readonly PushOptions _options;
    private readonly ILogger<WebPushDispatcher> _logger;

    public WebPushDispatcher(
        WebPushQueue queue,
        IServiceScopeFactory scopes,
        IHttpClientFactory httpClients,
        IOptions<PushOptions> options,
        ILogger<WebPushDispatcher> logger)
    {
        _queue = queue;
        _scopes = scopes;
        _httpClients = httpClients;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            if (!_options.WebPushEnabled)
                continue;

            try
            {
                var client = new WebPushClient(_httpClients.CreateClient(nameof(WebPushDispatcher)));
                var payload = JsonSerializer.Serialize(new { message.Title, message.Body, message.Url, message.Tag }, Json);
                await client.SendNotificationAsync(
                    new PushSubscription(message.Endpoint, message.P256dh, message.Auth),
                    payload,
                    new VapidDetails(_options.VapidSubject, _options.VapidPublicKey, _options.VapidPrivateKey),
                    stoppingToken);
            }
            catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
            {
                await ForgetAsync(message.Endpoint, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // The notification is in the in-app list either way
                _logger.LogWarning(ex, "Sending a browser push failed");
            }
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
