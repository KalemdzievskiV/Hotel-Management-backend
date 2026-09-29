using System.Threading.Channels;

namespace HotelManagement.Services.Push;

/// <summary>One push to one device</summary>
public record PushMessage(string Token, string Title, string Body, IReadOnlyDictionary<string, object?> Data);

/// <summary>
/// Pushes waiting to be sent. Requests only enqueue, so a slow or failing push service never
/// holds up or breaks the action that caused the notification.
/// </summary>
public interface IPushQueue
{
    void Enqueue(PushMessage message);
}

public class PushQueue : IPushQueue
{
    // Bounded so a push outage can't grow memory without limit; the oldest pushes go first
    private readonly Channel<PushMessage> _channel = Channel.CreateBounded<PushMessage>(
        new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public ChannelReader<PushMessage> Reader => _channel.Reader;

    public void Enqueue(PushMessage message) => _channel.Writer.TryWrite(message);
}
