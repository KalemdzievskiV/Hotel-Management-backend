namespace HotelManagement.Tests.Helpers;

/// <summary>
/// A clock tests can set and move forward, for trials, renewals and grace periods
/// </summary>
public class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan by) => Now += by;

    public DateTime UtcDateTime => Now.UtcDateTime;
}
