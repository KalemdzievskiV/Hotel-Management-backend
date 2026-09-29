using System.Collections.Concurrent;
using HotelManagement.Services.Push;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HotelManagement.Tests.Helpers;

/// <summary>
/// Keeps the pushes the app would send, instead of sending them to Expo
/// </summary>
public class RecordingPushQueue : IPushQueue
{
    private readonly ConcurrentQueue<PushMessage> _messages = new();

    public void Enqueue(PushMessage message) => _messages.Enqueue(message);

    public IReadOnlyList<PushMessage> SentTo(string token) => _messages.Where(m => m.Token == token).ToList();
}

/// <summary>
/// The test server with pushes recorded in <see cref="Pushes"/>
/// </summary>
public class PushRecordingWebApplicationFactory : CustomWebApplicationFactory
{
    public RecordingPushQueue Pushes { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushQueue>();
            services.AddSingleton<IPushQueue>(Pushes);
        });
    }
}
