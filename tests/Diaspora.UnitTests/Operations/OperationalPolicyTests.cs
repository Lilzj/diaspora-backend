using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The policy catalogue's composition rules, and each policy family's self-validation.
/// <para>
/// These are the checks that run before a host exists. Every one of them corresponds to a way a
/// deployment could ship a policy that looks reasonable and behaves wrongly — a retry whose ceiling is
/// below its floor, a breaker that can never close, a cache whose local tier outlives the shared one.
/// </para>
/// </summary>
public sealed class OperationalPolicyTests
{
    [Fact]
    public void A_policy_is_identified_by_its_kind_and_its_name_together()
    {
        var retry = new RetryPolicy("media.upload");
        var cache = new CachePolicy("media.upload");

        retry.Key.Should().NotBe(cache.Key,
            "two families may deliberately use one name for two concerns, and a catalogue that " +
            "collapsed them would let one shadow the other");
    }

    [Fact]
    public void A_higher_version_of_one_policy_replaces_a_lower_one()
    {
        var catalog = new OperationalPolicyCatalog(
        [
            new StubContributor("Platform", new RetryPolicy("external.provider") { MaxAttempts = 3 }),
            new StubContributor("Deployment", new RetryPolicy("external.provider", version: 2) { MaxAttempts = 7 })
        ]);

        catalog.Find<RetryPolicy>("external.provider")!.MaxAttempts.Should().Be(7,
            "raising the version is how a deployment overrides a platform default without editing it");
    }

    [Fact]
    public void A_lower_version_does_not_replace_a_higher_one_whatever_the_order()
    {
        var catalog = new OperationalPolicyCatalog(
        [
            new StubContributor("Deployment", new RetryPolicy("external.provider", version: 2) { MaxAttempts = 7 }),
            new StubContributor("Platform", new RetryPolicy("external.provider") { MaxAttempts = 3 })
        ]);

        catalog.Find<RetryPolicy>("external.provider")!.MaxAttempts.Should().Be(7,
            "the winner is decided by version, not by whichever assembly the scan reached last — " +
            "otherwise an override would depend on assembly load order");
    }

    [Fact]
    public void Two_declarations_at_one_version_are_a_conflict_rather_than_a_silent_winner()
    {
        var compose = () => new OperationalPolicyCatalog(
        [
            new StubContributor("A", new RetryPolicy("external.provider")),
            new StubContributor("B", new RetryPolicy("external.provider"))
        ]);

        compose.Should().Throw<InvalidOperationException>()
            .WithMessage("*declared at version 1 by both*",
                "a duplicate that resolves silently is a duplicate two components disagree about " +
                "for months");
    }

    [Fact]
    public void An_invalid_policy_stops_composition_and_names_its_declarer()
    {
        var compose = () => new OperationalPolicyCatalog(
        [
            new StubContributor("Reckless", new RetryPolicy("bad")
            {
                InitialDelay = TimeSpan.FromSeconds(10),
                MaxDelay = TimeSpan.FromSeconds(1)
            })
        ]);

        compose.Should().Throw<InvalidOperationException>()
            .WithMessage("*Reckless*MaxDelay is below InitialDelay*");
    }

    [Fact]
    public void An_unregistered_policy_resolves_to_the_callers_fallback()
    {
        var catalog = new OperationalPolicyCatalog([]);
        var fallback = new RetryPolicy("platform.default") { MaxAttempts = 2 };

        catalog.Get("nothing.here", fallback).Should().BeSameAs(fallback,
            "a component that has to null-check its own policy is one that will run with no policy " +
            "at all and nobody will notice");
    }

    [Theory]
    [InlineData(-1, 200, 5_000, 2.0, 0.2, "MaxAttempts cannot be negative.")]
    [InlineData(3, 5_000, 1_000, 2.0, 0.2, "MaxDelay is below InitialDelay, so the ceiling would clamp the first retry.")]
    [InlineData(3, 200, 5_000, 0.5, 0.2, "BackoffMultiplier below 1.0 makes each retry sooner than the last.")]
    [InlineData(3, 200, 5_000, 2.0, 1.5, "JitterRatio must be between 0 and 1.")]
    public void A_retry_policy_reports_each_way_it_can_be_wrong(
        int attempts, int initialMs, int maxMs, double multiplier, double jitter, string expected)
    {
        var policy = new RetryPolicy("test")
        {
            MaxAttempts = attempts,
            InitialDelay = TimeSpan.FromMilliseconds(initialMs),
            MaxDelay = TimeSpan.FromMilliseconds(maxMs),
            BackoffMultiplier = multiplier,
            JitterRatio = jitter
        };

        policy.Validate().Should().Contain(expected);
    }

    [Fact]
    public void A_breaker_that_permits_no_half_open_call_can_never_close()
    {
        var policy = new CircuitBreakerPolicy("test") { HalfOpenPermittedCalls = 0 };

        policy.Validate().Should().Contain(
            "HalfOpenPermittedCalls must be at least 1, or the circuit can never close.");
    }

    [Fact]
    public void A_breaker_with_no_minimum_throughput_would_trip_on_a_single_call()
    {
        var policy = new CircuitBreakerPolicy("test") { MinimumThroughput = 0 };

        policy.Validate().Should().Contain(
            "MinimumThroughput must be at least 1, or the ratio is computed over nothing.");
    }

    [Fact]
    public void A_cache_policy_with_no_absolute_expiry_is_refused()
    {
        var policy = new CachePolicy("test") { AbsoluteExpiration = TimeSpan.Zero };

        policy.Validate().Should().Contain(
            "AbsoluteExpiration must be positive; an entry with no absolute bound can be stale forever.");
    }

    [Fact]
    public void A_cache_policy_whose_local_tier_outlives_the_shared_one_is_refused()
    {
        var policy = new CachePolicy("test")
        {
            AbsoluteExpiration = TimeSpan.FromMinutes(1),
            LocalExpiration = TimeSpan.FromMinutes(5)
        };

        policy.Validate().Should().Contain(
            "LocalExpiration exceeds AbsoluteExpiration, so the local tier outlives the shared one.");
    }

    [Fact]
    public void A_distributed_cache_policy_may_not_declare_a_local_expiry()
    {
        var policy = new CachePolicy("test")
        {
            Tier = CacheTier.Distributed,
            LocalExpiration = TimeSpan.FromSeconds(30)
        };

        policy.Validate().Should().Contain(
            "LocalExpiration is only meaningful for a local or multi-level policy.",
            "this exact combination shipped in B17's own default for the personalized feed and was " +
            "caught by the catalogue at host start");
    }

    [Fact]
    public void A_queue_policy_that_claims_adaptive_throughput_must_have_room_to_adapt()
    {
        var policy = new QueuePolicy("test")
        {
            Concurrency = 4,
            MaxConcurrency = 4,
            AdaptiveThroughput = true
        };

        policy.Validate().Should().Contain(
            "AdaptiveThroughput is enabled but MaxConcurrency equals Concurrency, leaving nothing to adapt.");
    }

    [Fact]
    public void A_recovery_policy_whose_backups_expire_before_its_recovery_point_cannot_meet_it()
    {
        var policy = new RecoveryPolicy("test")
        {
            RecoveryPointObjective = TimeSpan.FromDays(10),
            BackupRetention = TimeSpan.FromDays(1)
        };

        policy.Validate().Should().Contain(
            "BackupRetention is shorter than the recovery point objective, so no restore can meet it.");
    }

    [Fact]
    public void A_burst_capacity_outside_a_token_bucket_is_reported_rather_than_ignored()
    {
        var policy = new RateLimitPolicy("test")
        {
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            BurstCapacity = 20
        };

        policy.Validate().Should().Contain(
            "BurstCapacity is only honoured by the token-bucket algorithm.",
            "a setting that is silently ignored is worse than one that is refused: somebody " +
            "configured a burst and believes they have one");
    }

    private sealed class StubContributor : IOperationalPolicyContributor
    {
        private readonly OperationalPolicy[] _policies;

        public StubContributor(string module, params OperationalPolicy[] policies)
        {
            Module = module;
            _policies = policies;
        }

        public string Module { get; }

        public IEnumerable<OperationalPolicy> Policies => _policies;
    }
}
