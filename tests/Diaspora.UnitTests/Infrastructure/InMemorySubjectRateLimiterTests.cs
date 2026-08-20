using Diaspora.Infrastructure.Services;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Infrastructure;

/// <summary>
/// The subject limiter is the defence against a distributed attack on one phone number —
/// the case a per-IP limiter misses entirely. These tests pin its budget, its per-key
/// isolation, its window recovery and its reset.
/// </summary>
public class InMemorySubjectRateLimiterTests
{
    private static readonly DateTimeOffset Start = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task It_allows_up_to_the_permit_limit_then_refuses()
    {
        var clock = new FakeClock(Start);
        var limiter = new InMemorySubjectRateLimiter(clock);
        var window = TimeSpan.FromMinutes(15);

        for (var i = 0; i < 3; i++)
        {
            (await limiter.AcquireAsync("signin:+2348012345678", 3, window)).IsAllowed.Should().BeTrue();
        }

        var refused = await limiter.AcquireAsync("signin:+2348012345678", 3, window);

        refused.IsAllowed.Should().BeFalse();
        refused.RetryAfter.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Budgets_are_isolated_per_key()
    {
        var limiter = new InMemorySubjectRateLimiter(new FakeClock(Start));
        var window = TimeSpan.FromMinutes(15);

        await limiter.AcquireAsync("signin:+111", 1, window);
        await limiter.AcquireAsync("signin:+111", 1, window); // +111 now exhausted

        // A different number is untouched — one member's attacker does not throttle another.
        (await limiter.AcquireAsync("signin:+222", 1, window)).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task The_window_slides_so_old_attempts_stop_counting()
    {
        var clock = new FakeClock(Start);
        var limiter = new InMemorySubjectRateLimiter(clock);
        var window = TimeSpan.FromMinutes(15);

        await limiter.AcquireAsync("signin:+111", 1, window); // exhausted
        (await limiter.AcquireAsync("signin:+111", 1, window)).IsAllowed.Should().BeFalse();

        // Advance past the window; the earlier attempt falls out of it.
        clock.Advance(TimeSpan.FromMinutes(16));

        (await limiter.AcquireAsync("signin:+111", 1, window)).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Reset_clears_a_budget_immediately()
    {
        var limiter = new InMemorySubjectRateLimiter(new FakeClock(Start));
        var window = TimeSpan.FromMinutes(15);

        await limiter.AcquireAsync("signin:+111", 1, window);
        (await limiter.AcquireAsync("signin:+111", 1, window)).IsAllowed.Should().BeFalse();

        // A successful sign-in resets the key so a member who mistyped their password is not
        // left throttled afterwards.
        await limiter.ResetAsync("signin:+111");

        (await limiter.AcquireAsync("signin:+111", 1, window)).IsAllowed.Should().BeTrue();
    }
}
