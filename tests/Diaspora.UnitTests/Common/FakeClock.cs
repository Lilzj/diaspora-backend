using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.UnitTests.Common;

public sealed class FakeClock : IClock
{
    private DateTimeOffset _now;

    public FakeClock(DateTimeOffset? initialTime = null)
    {
        _now = initialTime ?? new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    }

    public DateTimeOffset UtcNow => _now;

    public DateTimeOffset Now(string timeZoneId)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        return TimeZoneInfo.ConvertTime(_now, tz);
    }

    public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    public void Set(DateTimeOffset time) => _now = time;
}
