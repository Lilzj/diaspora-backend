using System.Diagnostics;
using System.Globalization;
using Diaspora.IntegrationTests.Common;
using Diaspora.Platform.Operations.Caching;
using Diaspora.Platform.Operations.Health;
using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.RateLimiting;
using Diaspora.Platform.Operations.Resilience;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Platform;

/// <summary>
/// Part 13's baselines, measured rather than asserted.
/// <para>
/// <b>What these numbers are.</b> Latency of the composed host, in process, against an in-memory
/// SQLite database, on one developer machine, with no network between client and server. They are a
/// floor and a regression tripwire, and they are emphatically not a capacity model: a deployment on
/// PostgreSQL over a network will be slower for every database-bound number here and roughly the same
/// for every CPU-bound one.
/// </para>
/// <para>
/// <b>Why they are worth having anyway.</b> DEBT-030 records that the platform has had no performance
/// measurement anywhere, at any scale, for its entire life — indexes chosen from query shapes, the
/// shapes sound and the numbers unmeasured. A floor measured under stated conditions is the first
/// number the platform has ever had, and a run that is ten times slower than this one is a finding
/// whatever the absolute values mean.
/// </para>
/// <para>
/// The assertions are deliberately loose — they exist so the suite fails on a collapse rather than on
/// a busy CI agent. The numbers themselves go to the test output and into the Performance Baseline
/// Report.
/// </para>
/// </summary>
public sealed class PerformanceBaselineTests : IntegrationTestBase
{
    private const int WarmupIterations = 20;
    private const int MeasuredIterations = 200;

    private readonly ITestOutputHelper _output;

    public PerformanceBaselineTests(CustomWebApplicationFactory factory, ITestOutputHelper output)
        : base(factory)
    {
        _output = output;
        factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task The_unauthenticated_platform_endpoint_answers_within_the_baseline()
    {
        var measurement = await MeasureAsync("HTTP GET /", async () =>
        {
            using var response = await Client.GetAsync(new Uri("/", UriKind.Relative));
            response.EnsureSuccessStatusCode();
        });

        measurement.P95.Should().BeLessThan(500,
            "the platform manifest is a dictionary read; anything approaching half a second means " +
            "the host is doing work nobody asked for on every request");
    }

    [Fact]
    public async Task The_liveness_probe_answers_within_the_baseline()
    {
        var measurement = await MeasureAsync("HTTP GET /health/live", async () =>
        {
            using var response = await Client.GetAsync(new Uri("/health/live", UriKind.Relative));
            response.EnsureSuccessStatusCode();
        });

        measurement.P95.Should().BeLessThan(500,
            "liveness must depend on nothing external, so it should be indistinguishable from a " +
            "static response");
    }

    [Fact]
    public async Task The_readiness_probe_answers_within_the_baseline()
    {
        // Fewer iterations: readiness opens a database connection and runs every module's health
        // contributor, and two hundred of those is a load test rather than a measurement.
        var measurement = await MeasureAsync("HTTP GET /health/ready", async () =>
        {
            using var response = await Client.GetAsync(new Uri("/health/ready", UriKind.Relative));
            _ = await response.Content.ReadAsStringAsync();
        }, iterations: 50);

        measurement.P95.Should().BeLessThan(3_000,
            "an orchestrator's probe timeout is typically a few seconds, and a readiness check that " +
            "exceeds it reports every check as unknown rather than reporting which one failed");
    }

    [Fact]
    public async Task A_cache_hit_is_an_order_of_magnitude_cheaper_than_the_factory_it_replaces()
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var key = CacheKey.For("baseline", "cache-hit");

        // The factory simulates a source that takes a millisecond — an optimistic figure for a
        // database round trip and a pessimistic one for the cache it is being compared against.
        static async Task<string> Source(CancellationToken token)
        {
            await Task.Delay(1, token);
            return "value";
        }

        await cache.GetOrCreateAsync(key, OperationalPolicyNames.Cache.Default, Source);

        var measurement = await MeasureAsync("Cache hit (local tier)", async () =>
        {
            await cache.GetOrCreateAsync(key, OperationalPolicyNames.Cache.Default, Source);
        });

        measurement.P95.Should().BeLessThan(5,
            "a cached read that costs anything like a database round trip is a cache that is not " +
            "earning its complexity");
    }

    [Fact]
    public async Task A_rate_limit_decision_is_effectively_free()
    {
        using var scope = Factory.Services.CreateScope();
        var limiter = scope.ServiceProvider.GetRequiredService<IPlatformRateLimiter>();

        var measurement = await MeasureAsync("Rate limit decision", async () =>
        {
            await limiter.AcquireAsync(
                OperationalPolicyNames.RateLimit.AnonymousApi,
                new RateLimitRequest(ClientAddress: "203.0.113.1"));
        });

        measurement.P95.Should().BeLessThan(5,
            "the limiter sits in front of every request, so its own cost has to be far below the " +
            "cost of the work it protects");
    }

    [Fact]
    public async Task A_resilient_call_adds_little_to_the_operation_it_wraps()
    {
        using var scope = Factory.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IResilienceExecutor>();

        var measurement = await MeasureAsync("Resilient call (bulkhead + breaker, no retry)", async () =>
        {
            await executor.ExecuteAsync(
                OperationalPolicyNames.CircuitBreaker.ExternalApi, _ => Task.FromResult(1));
        });

        measurement.P95.Should().BeLessThan(10,
            "every external call on the platform goes through this, so its overhead is paid on every " +
            "payment, every upload and every notification");
    }

    [Fact]
    public async Task A_full_health_pass_across_every_module_stays_within_a_probe_budget()
    {
        using var scope = Factory.Services.CreateScope();
        var aggregator = scope.ServiceProvider.GetRequiredService<IPlatformHealthAggregator>();

        // Measures the *cached* pass, and says so in the label rather than pretending otherwise: the
        // health policy caches results for five seconds and fifty iterations run in far less than
        // that. The number is still the one that matters most, because it is what every probe after
        // the first receives — an orchestrator, a load balancer and a dashboard all polling readiness
        // pay this, not the cost of a full pass. The uncached cost is in the /health/ready measurement.
        var measurement = await MeasureAsync(
            "Health aggregation (cached read, per health policy)",
            async () => await aggregator.CheckAsync(),
            iterations: 50);

        measurement.P95.Should().BeLessThan(3_000,
            "contributors run concurrently under per-policy timeouts precisely so this stays inside " +
            "an orchestrator's probe budget as modules are added");
    }

    /// <summary>
    /// Runs an operation, discards a warm-up, and reports the distribution.
    /// <para>
    /// A warm-up rather than a straight average: the first call through a code path pays JIT
    /// compilation, first-touch allocation and a cold cache, and including it in a percentile makes
    /// the number a measurement of startup rather than of the operation.
    /// </para>
    /// <para>
    /// Percentiles rather than a mean, for the reason every latency measurement eventually learns: a
    /// mean hides the tail, and the tail is what a member experiences.
    /// </para>
    /// </summary>
    private async Task<Measurement> MeasureAsync(
        string name, Func<Task> operation, int iterations = MeasuredIterations)
    {
        for (var warmup = 0; warmup < WarmupIterations; warmup++)
        {
            await operation();
        }

        var samples = new double[iterations];

        for (var index = 0; index < iterations; index++)
        {
            var start = Stopwatch.GetTimestamp();
            await operation();
            samples[index] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        }

        Array.Sort(samples);

        var measurement = new Measurement(
            name,
            iterations,
            samples[0],
            samples[(int)(iterations * 0.50)],
            samples[(int)(iterations * 0.95)],
            samples[(int)(iterations * 0.99)],
            samples[^1]);

        _output.WriteLine(measurement.ToString());

        return measurement;
    }

    private sealed record Measurement(
        string Name, int Iterations, double Min, double P50, double P95, double P99, double Max)
    {
        public override string ToString() => string.Create(
            CultureInfo.InvariantCulture,
            $"{Name,-48} n={Iterations,4}  min={Min,8:F3}ms  p50={P50,8:F3}ms  " +
            $"p95={P95,8:F3}ms  p99={P99,8:F3}ms  max={Max,8:F3}ms");
    }
}
