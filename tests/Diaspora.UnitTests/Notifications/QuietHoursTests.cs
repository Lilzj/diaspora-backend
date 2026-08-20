using Diaspora.Modules.Notifications.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Notifications;

/// <summary>
/// Quiet hours, and specifically the wrap.
/// <para>
/// <b>Every real quiet-hours window crosses midnight.</b> "22:00 to 07:00" is what a person
/// configures, and a containment check written as <c>hour &gt;= start &amp;&amp; hour &lt; end</c> is
/// silently false for all of it — which means a platform that "supports quiet hours" pushes at 3am
/// and nobody notices until members leave.
/// </para>
/// </summary>
public class QuietHoursTests
{
    [Theory]
    [InlineData(22)]
    [InlineData(23)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void A_window_that_wraps_midnight_contains_the_whole_night(int hour)
    {
        var quietHours = QuietHours.TryCreate(isEnabled: true, startHour: 22, endHour: 7);

        quietHours.Should().NotBeNull();
        quietHours!.Contains(hour).Should().BeTrue(
            "22:00 to 07:00 covers {0}:00, and a naive range check says it does not", hour);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(21)]
    public void A_window_that_wraps_midnight_excludes_the_day(int hour)
    {
        var quietHours = QuietHours.TryCreate(isEnabled: true, startHour: 22, endHour: 7);

        quietHours!.Contains(hour).Should().BeFalse();
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(13, true)]
    [InlineData(9, false)]
    [InlineData(14, false)]
    public void A_window_inside_one_day_behaves_normally(int hour, bool expected)
    {
        var quietHours = QuietHours.TryCreate(isEnabled: true, startHour: 10, endHour: 14);

        quietHours!.Contains(hour).Should().Be(expected);
    }

    /// <summary>
    /// Equal hours are refused because they could equally mean "never" and "always", and a
    /// preference that ambiguous is a bug waiting to be argued about.
    /// </summary>
    [Fact]
    public void Equal_start_and_end_hours_are_refused()
    {
        QuietHours.TryCreate(isEnabled: true, startHour: 9, endHour: 9).Should().BeNull();
    }

    [Theory]
    [InlineData(-1, 7)]
    [InlineData(22, 24)]
    public void Hours_outside_the_clock_are_refused(int start, int end)
    {
        QuietHours.TryCreate(isEnabled: true, start, end).Should().BeNull();
    }

    [Fact]
    public void A_disabled_window_contains_nothing()
    {
        QuietHours.Disabled().Contains(3).Should().BeFalse();
    }

    /// <summary>
    /// The end of the window is the moment a deferred message is released. Inside the night, that is
    /// tomorrow morning — not this morning, which has already passed.
    /// </summary>
    [Fact]
    public void The_next_end_after_midnight_is_this_morning()
    {
        var quietHours = QuietHours.TryCreate(isEnabled: true, startHour: 22, endHour: 7)!;
        var localNow = new DateTimeOffset(2026, 3, 14, 2, 30, 0, TimeSpan.Zero);

        quietHours.NextEndAfter(localNow)
            .Should().Be(new DateTimeOffset(2026, 3, 14, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void The_next_end_before_midnight_is_tomorrow_morning()
    {
        var quietHours = QuietHours.TryCreate(isEnabled: true, startHour: 22, endHour: 7)!;
        var localNow = new DateTimeOffset(2026, 3, 14, 23, 15, 0, TimeSpan.Zero);

        quietHours.NextEndAfter(localNow)
            .Should().Be(new DateTimeOffset(2026, 3, 15, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void A_moment_outside_the_window_is_not_moved()
    {
        var quietHours = QuietHours.TryCreate(isEnabled: true, startHour: 22, endHour: 7)!;
        var localNow = new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero);

        quietHours.NextEndAfter(localNow).Should().Be(localNow);
    }
}
