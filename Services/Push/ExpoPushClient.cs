using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace HotelManagement.Services.Push;

public interface IExpoPushClient
{
    /// <summary>
    /// Sends up to 100 pushes in one request. Returns the tokens Expo says no longer reach a
    /// device (the app was uninstalled or its token changed); they should be forgotten.
    /// </summary>
    Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken);
}

/// <summary>
/// Expo's push API (https://docs.expo.dev/push-notifications/sending-notifications/). Expo
/// forwards to FCM (Android) and APNs (iOS) with the credentials uploaded to the EAS project.
/// </summary>
public class ExpoPushClient : IExpoPushClient
{
    public const int MaxBatchSize = 100;
    private const string SendUrl = "https://exp.host/--/api/v2/push/send";

    /// <summary>The Android channel the app creates; pushes to another channel id are dropped</summary>
    public const string AndroidChannelId = "default";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly PushOptions _options;

    public ExpoPushClient(HttpClient http, IOptions<PushOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<string>> SendAsync(IReadOnlyList<PushMessage> messages, CancellationToken cancellationToken)
    {
        var payload = messages
            .Select(m => new ExpoMessage(m.Token, m.Title, m.Body, m.Data, "default", "high", AndroidChannelId))
            .ToList();

        using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl)
        {
            Content = JsonContent.Create(payload, options: Json)
        };
        if (!string.IsNullOrEmpty(_options.ExpoAccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ExpoAccessToken);

        using var response = await _http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ExpoResponse>(Json, cancellationToken);
        var tickets = result?.Data ?? [];

        // Tickets come back in the order the messages were sent
        var gone = new List<string>();
        for (var i = 0; i < tickets.Count && i < messages.Count; i++)
        {
            if (tickets[i].Status == "error" && tickets[i].Details?.Error == "DeviceNotRegistered")
                gone.Add(messages[i].Token);
        }
        return gone;
    }

    private record ExpoMessage(
        string To,
        string Title,
        string Body,
        IReadOnlyDictionary<string, object?> Data,
        string Sound,
        string Priority,
        string ChannelId);

    private record ExpoResponse(List<ExpoTicket>? Data);

    private record ExpoTicket(string Status, string? Message, ExpoTicketDetails? Details);

    private record ExpoTicketDetails(string? Error);
}
