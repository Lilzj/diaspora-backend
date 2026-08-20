using Diaspora.Operations.Caching;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Operations;

/// <summary>
/// The platform cache's behaviour, against a real store.
/// <para>
/// Real stores rather than substitutes throughout, because every property worth asserting here is
/// about the interaction between the tiers, the tag versions and the policy — and a substituted store
/// asserts only that the cache calls the methods the test author expected it to call.
/// </para>
/// </summary>
public sealed class PlatformCacheServiceTests
{
    private const string PolicyName = "test.policy";

    [Fact]
    public async Task A_miss_runs_the_factory_and_a_hit_does_not()
    {
        var cache = Build(out _);
        var calls = 0;

        var first = await cache.GetOrCreateAsync(
            CacheKey.For("test", "one"), PolicyName, _ => { calls++; return Task.FromResult("value"); });

        var second = await cache.GetOrCreateAsync(
            CacheKey.For("test", "one"), PolicyName, _ => { calls++; return Task.FromResult("value"); });

        first.Should().Be("value");
        second.Should().Be("value");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_misses_on_one_key_run_the_factory_once()
    {
        var cache = Build(out _);
        var calls = 0;
        var release = new TaskCompletionSource();

        async Task<string> Factory(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            await release.Task;
            return "value";
        }

        var key = CacheKey.For("test", "stampede");

        var readers = Enumerable.Range(0, 20)
            .Select(_ => cache.GetOrCreateAsync(key, PolicyName, Factory))
            .ToArray();

        release.SetResult();

        var results = await Task.WhenAll(readers);

        results.Should().AllBe("value");
        calls.Should().Be(1,
            "without stampede protection a popular key expiring sends every in-flight request to the " +
            "database at the same instant, which turns the cache from a load reducer into a load " +
            "amplifier at exactly the wrong moment");
    }

    [Fact]
    public async Task Null_is_never_cached()
    {
        var cache = Build(out _);
        var calls = 0;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await cache.GetOrCreateAsync<string?>(
                CacheKey.For("test", "absent"), PolicyName, _ => { calls++; return Task.FromResult<string?>(null); });
        }

        calls.Should().Be(3,
            "caching null makes 'this lookup found nothing' indistinguishable from 'this lookup has " +
            "not run', and the entry that says nothing survives the write that would have made it " +
            "something");
    }

    [Fact]
    public async Task An_entry_past_its_absolute_expiry_is_a_miss()
    {
        var cache = Build(out var clock);
        var key = CacheKey.For("test", "expiring");

        await cache.SetAsync(key, "value", PolicyName);

        clock.Advance(TimeSpan.FromMinutes(10));

        (await cache.GetAsync<string>(key, PolicyName)).Should().BeNull();
    }

    [Fact]
    public async Task Invalidating_a_namespace_drops_every_entry_in_it_and_nothing_else()
    {
        var cache = Build(out _);

        await cache.SetAsync(CacheKey.For("alpha", "one"), "a1", PolicyName);
        await cache.SetAsync(CacheKey.For("alpha", "two"), "a2", PolicyName);
        await cache.SetAsync(CacheKey.For("beta", "one"), "b1", PolicyName);

        await cache.RemoveNamespaceAsync("alpha");

        (await cache.GetAsync<string>(CacheKey.For("alpha", "one"), PolicyName)).Should().BeNull();
        (await cache.GetAsync<string>(CacheKey.For("alpha", "two"), PolicyName)).Should().BeNull();
        (await cache.GetAsync<string>(CacheKey.For("beta", "one"), PolicyName)).Should().Be("b1");
    }

    [Fact]
    public async Task Invalidating_a_tag_drops_entries_written_under_it()
    {
        var cache = Build(out var clock, policy => policy with { Tags = ["guides"] });

        await cache.SetAsync(CacheKey.For("test", "tagged"), "value", PolicyName);

        // The version store caches a scope for the refresh interval, so a bump inside that window is
        // invisible to a read on another replica. This process performed the bump, so its own local
        // copy is updated immediately — but advancing the clock past the interval makes the assertion
        // independent of that shortcut.
        await ((ICacheInvalidator)cache).InvalidateTagAsync("guides");
        clock.Advance(TimeSpan.FromSeconds(30));

        (await cache.GetAsync<string>(CacheKey.For("test", "tagged"), PolicyName)).Should().BeNull();
    }

    [Fact]
    public async Task A_tag_invalidation_does_not_touch_entries_without_that_tag()
    {
        var cache = Build(out _, policy => policy with { Tags = ["guides"] });

        await cache.SetAsync(CacheKey.For("test", "tagged"), "value", PolicyName);

        await ((ICacheInvalidator)cache).InvalidateTagAsync("providers");

        (await cache.GetAsync<string>(CacheKey.For("test", "tagged"), PolicyName)).Should().Be("value");
    }

    [Fact]
    public async Task Statistics_separate_hits_from_misses_per_namespace()
    {
        var cache = Build(out _);

        await cache.GetOrCreateAsync(CacheKey.For("alpha", "k"), PolicyName, _ => Task.FromResult("v"));
        await cache.GetOrCreateAsync(CacheKey.For("alpha", "k"), PolicyName, _ => Task.FromResult("v"));
        await cache.GetOrCreateAsync(CacheKey.For("beta", "k"), PolicyName, _ => Task.FromResult("v"));

        var snapshot = ((ICacheStatisticsProvider)cache).Snapshot();

        snapshot.Namespaces.Single(entry => entry.Namespace == "alpha").Hits.Should().Be(1);
        snapshot.Namespaces.Single(entry => entry.Namespace == "alpha").Misses.Should().Be(1);
        snapshot.Namespaces.Single(entry => entry.Namespace == "beta").Misses.Should().Be(1);
    }

    [Fact]
    public void The_snapshot_reports_when_no_shared_store_is_registered()
    {
        var cache = Build(out _);

        var snapshot = ((ICacheStatisticsProvider)cache).Snapshot();

        snapshot.IsDistributed.Should().BeFalse();
        snapshot.DistributedProvider.Should().BeNull(
            "a multi-level policy served entirely from process memory is inconsistent across " +
            "replicas, and an operator must be able to see that rather than infer it from a healthy " +
            "hit ratio");
    }

    [Fact]
    public async Task A_disabled_cache_always_runs_the_factory()
    {
        var cache = Build(out _, configure: options => options.Enabled = false);
        var calls = 0;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            await cache.GetOrCreateAsync(
                CacheKey.For("test", "k"), PolicyName, _ => { calls++; return Task.FromResult("v"); });
        }

        calls.Should().Be(3);
    }

    [Fact]
    public async Task An_unknown_policy_name_falls_back_rather_than_throwing()
    {
        var cache = Build(out _);

        var value = await cache.GetOrCreateAsync(
            CacheKey.For("test", "k"), "no.such.policy", _ => Task.FromResult("v"));

        value.Should().Be("v", "a cache is not where a request should die");
    }

    [Fact]
    public void A_key_with_no_namespace_is_refused()
    {
        var cache = Build(out _);

        var read = () => cache.GetAsync<string>(default, PolicyName);

        read.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public void A_null_part_is_distinguished_from_an_absent_one()
    {
        var withNull = CacheKey.For("test", "a", null, "b");
        var without = CacheKey.For("test", "a", "b");

        withNull.Should().NotBe(without,
            "skipping a null part would make two different questions share one key, which is how a " +
            "cache starts answering one with the other's answer");
    }

    [Fact]
    public void A_part_containing_the_separator_cannot_shift_the_other_segments()
    {
        var injected = CacheKey.For("test", "a:b", "c");
        var plain = CacheKey.For("test", "a", "b", "c");

        injected.Should().NotBe(plain);
    }

    private static PlatformCacheService Build(
        out FakeClock clock,
        Func<CachePolicy, CachePolicy>? tune = null,
        Action<CacheOptions>? configure = null)
    {
        clock = new FakeClock();

        var options = new CacheOptions
        {
            KeyPrefix = "test",
            VersionRefreshInterval = TimeSpan.FromSeconds(5)
        };

        configure?.Invoke(options);

        var wrapped = Options.Create(options);
        var store = new MemoryCacheStore(wrapped, clock);

        var policy = new CachePolicy(PolicyName)
        {
            Tier = CacheTier.Local,
            AbsoluteExpiration = TimeSpan.FromMinutes(5),
            LocalExpiration = TimeSpan.FromMinutes(5)
        };

        var catalog = new OperationalPolicyCatalog(
            [new PolicyContributor(tune is null ? policy : tune(policy))]);

        return new PlatformCacheService(
            [store],
            new CacheVersionStore(store, clock, wrapped),
            catalog,
            clock,
            wrapped,
            NullLogger<PlatformCacheService>.Instance);
    }

    private sealed class PolicyContributor : IOperationalPolicyContributor
    {
        private readonly OperationalPolicy _policy;

        public PolicyContributor(OperationalPolicy policy) => _policy = policy;

        public string Module => "Test";

        public IEnumerable<OperationalPolicy> Policies => [_policy];
    }
}
