using Diaspora.Operations.RateLimiting;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.RateLimiting;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The platform limiter, one algorithm at a time.
/// <para>
/// Every case here is a way a limit can be wrong in production and right in a naive test: a fixed
/// window that permits double the rate across its boundary, a token bucket that never refills, a
/// concurrency limit that is never released, a partition that is shared between two subjects.
/// </para>
/// </summary>
public sealed class PlatformRateLimiterTests
{
    private const string Policy = "test.limit";

    [Fact]
    public async Task An_unknown_policy_allows_the_call()
    {
        var limiter = Build(out _, new RateLimitPolicy(Policy));

        var decision = await limiter.AcquireAsync("no.such.policy", new RateLimitRequest());

        decision.IsAllowed.Should().BeTrue(
            "a limiter that refused everything on a typo would take the platform down over a " +
            "misspelling, and the failure would look like an outage rather than a configuration error");
    }

    [Fact]
    public async Task A_disabled_policy_allows_the_call()
    {
        var limiter = Build(out _, new RateLimitPolicy(Policy) { PermitLimit = 1, Enabled = false });

        for (var attempt = 0; attempt < 5; attempt++)
        {
            (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
        }
    }

    [Fact]
    public async Task A_fixed_window_refuses_past_its_limit_and_resets_when_it_rolls()
    {
        var limiter = Build(out var clock, new RateLimitPolicy(Policy)
        {
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 2,
            Window = TimeSpan.FromMinutes(1)
        });

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeFalse();

        clock.Advance(TimeSpan.FromMinutes(1));

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_sliding_window_does_not_permit_a_double_rate_burst_across_its_boundary()
    {
        var limiter = Build(out var clock, new RateLimitPolicy(Policy)
        {
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            PermitLimit = 6,
            SegmentsPerWindow = 6,
            Window = TimeSpan.FromMinutes(1)
        });

        // Spend the whole budget at the end of one notional window.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
        }

        // A fixed window would reset here and permit six more, making twelve requests in ten seconds
        // against a limit of six a minute — entirely within the rules and exactly the abuse a sliding
        // window exists to refuse.
        clock.Advance(TimeSpan.FromSeconds(10));

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeFalse();

        clock.Advance(TimeSpan.FromSeconds(51));

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task A_token_bucket_allows_a_burst_and_then_refills_over_time()
    {
        var limiter = Build(out var clock, new RateLimitPolicy(Policy)
        {
            Algorithm = RateLimitAlgorithm.TokenBucket,
            PermitLimit = 60,
            BurstCapacity = 10,
            Window = TimeSpan.FromMinutes(1)
        });

        // Capacity is limit plus burst: a foregrounding mobile client legitimately fires a screen's
        // worth of requests at once and is not an attacker.
        for (var attempt = 0; attempt < 70; attempt++)
        {
            (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
        }

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeFalse();

        clock.Advance(TimeSpan.FromSeconds(2));

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue(
            "one token a second at sixty a minute means two seconds buys two calls");
    }

    [Fact]
    public async Task A_concurrency_limit_frees_when_a_call_is_released()
    {
        var limiter = Build(out _, new RateLimitPolicy(Policy)
        {
            Algorithm = RateLimitAlgorithm.Concurrency,
            PermitLimit = 2
        });

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue();
        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeFalse();

        await limiter.ReleaseAsync(Policy, Request());

        (await limiter.AcquireAsync(Policy, Request())).IsAllowed.Should().BeTrue(
            "a concurrency limit that is never released permits exactly PermitLimit calls for the " +
            "life of the process — a failure that looks like a working limiter for the first minute");
    }

    [Fact]
    public async Task Two_subjects_do_not_share_a_budget()
    {
        var limiter = Build(out _, new RateLimitPolicy(Policy)
        {
            Dimension = RateLimitDimension.Subject,
            PermitLimit = 1,
            Window = TimeSpan.FromMinutes(1)
        });

        (await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "a"))).IsAllowed.Should().BeTrue();
        (await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "a"))).IsAllowed.Should().BeFalse();
        (await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "b"))).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task An_unattributable_call_shares_one_bucket_rather_than_escaping_the_limit()
    {
        var limiter = Build(out _, new RateLimitPolicy(Policy)
        {
            Dimension = RateLimitDimension.User,
            PermitLimit = 1,
            Window = TimeSpan.FromMinutes(1)
        });

        (await limiter.AcquireAsync(Policy, new RateLimitRequest())).IsAllowed.Should().BeTrue();
        (await limiter.AcquireAsync(Policy, new RateLimitRequest())).IsAllowed.Should().BeFalse(
            "no limit when the dimension cannot be resolved means an attacker who strips the header " +
            "that carries it is unlimited");
    }

    [Fact]
    public async Task A_reset_clears_the_budget()
    {
        var limiter = Build(out _, new RateLimitPolicy(Policy)
        {
            Dimension = RateLimitDimension.Subject,
            PermitLimit = 1,
            Window = TimeSpan.FromMinutes(1)
        });

        await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "a"));
        (await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "a"))).IsAllowed.Should().BeFalse();

        await limiter.ResetAsync(Policy, new RateLimitRequest(Subject: "a"));

        (await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "a"))).IsAllowed.Should().BeTrue(
            "a member who mistyped their password four times must not still be throttled after a " +
            "successful sign-in");
    }

    [Fact]
    public async Task Idle_partitions_are_reclaimed_and_busy_ones_are_not()
    {
        var limiter = Build(out var clock, new RateLimitPolicy(Policy)
        {
            Dimension = RateLimitDimension.Subject,
            Algorithm = RateLimitAlgorithm.Concurrency,
            PermitLimit = 4
        });

        await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "idle"));
        await limiter.ReleaseAsync(Policy, new RateLimitRequest(Subject: "idle"));

        // Taken and never released: a partition with calls in flight is never idle, however long ago
        // it was touched, because discarding it would lose the count and let the next caller exceed
        // the limit.
        await limiter.AcquireAsync(Policy, new RateLimitRequest(Subject: "busy"));

        clock.Advance(TimeSpan.FromHours(1));

        limiter.Compact(TimeSpan.FromMinutes(15)).Should().Be(1);
        limiter.BucketCount.Should().Be(1);
    }

    private static RateLimitRequest Request() => new(Subject: "subject");

    private static PlatformRateLimiter Build(out FakeClock clock, RateLimitPolicy policy)
    {
        clock = new FakeClock();

        return new PlatformRateLimiter(
            new OperationalPolicyCatalog([new Contributor(policy)]), clock);
    }

    private sealed class Contributor : IOperationalPolicyContributor
    {
        private readonly OperationalPolicy _policy;

        public Contributor(OperationalPolicy policy) => _policy = policy;

        public string Module => "Test";

        public IEnumerable<OperationalPolicy> Policies => [_policy];
    }
}
