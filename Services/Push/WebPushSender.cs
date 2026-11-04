using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebPush;

namespace HotelManagement.Services.Push;

public enum WebPushSendStatus
{
    Sent,
    /// <summary>The push service no longer knows this browser; the subscription should be deleted</summary>
    Gone,
    Failed
}

public record WebPushSendResult(WebPushSendStatus Status, string? Error = null);

/// <summary>Sends one browser push right away, signed with the VAPID keys</summary>
public interface IWebPushSender
{
    Task<WebPushSendResult> SendAsync(WebPushMessage message, CancellationToken cancellationToken = default);
}

public class WebPushSender : IWebPushSender
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClients;
    private readonly PushOptions _options;

    public WebPushSender(IHttpClientFactory httpClients, IOptions<PushOptions> options)
    {
        _httpClients = httpClients;
        _options = options.Value;
    }

    public async Task<WebPushSendResult> SendAsync(WebPushMessage message, CancellationToken cancellationToken = default)
    {
        if (!_options.WebPushEnabled)
            return new(WebPushSendStatus.Failed, "Web push isn't set up on the server");

        try
        {
            var client = new WebPushClient(_httpClients.CreateClient(nameof(WebPushSender)));
            var payload = JsonSerializer.Serialize(new { message.Title, message.Body, message.Url, message.Tag }, Json);
            await client.SendNotificationAsync(
                new PushSubscription(message.Endpoint, message.P256dh, message.Auth),
                payload,
                new VapidDetails(_options.VapidSubject, _options.VapidPublicKey, _options.VapidPrivateKey),
                cancellationToken);
            return new(WebPushSendStatus.Sent);
        }
        catch (WebPushException ex) when (ex.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
        {
            return new(WebPushSendStatus.Gone, $"{(int)ex.StatusCode} {ex.Message}");
        }
        catch (WebPushException ex)
        {
            return new(WebPushSendStatus.Failed, $"{(int)ex.StatusCode} {ex.Message}");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            return new(WebPushSendStatus.Failed, ex.Message);
        }
    }
}
