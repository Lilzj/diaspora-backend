using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Search;
using Diaspora.Modules.Location.Domain.Spatial;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// B4.1 — the aggregate endpoints ask their contributors one at a time.
/// <para>
/// <b>Why this is a unit test and not an integration one.</b> Every shipped counter and nearby
/// provider answers from the platform's single scoped <c>DiasporaDbContext</c>, and EF Core refuses
/// a second operation on a context while the first is in flight. The original
/// <c>Task.WhenAll</c> fan-out therefore threw on every contributor but one, and both aggregators
/// turn a throw into "this vertical could not be asked" — so a neighbourhood hub rendered one
/// number out of six and reported nothing wrong.
/// </para>
/// <para>
/// <b>The integration suite cannot reproduce it.</b> It runs on SQLite, whose ADO.NET provider
/// implements the async methods synchronously: each contributor completes before the next begins
/// and the overlap never happens. Production runs on Npgsql, which is genuinely asynchronous, so
/// the failure appears only where nobody is watching for it. The property that matters — that two
/// contributors are never in flight at once — is therefore asserted directly, against fakes that
/// observe their own overlap, and it holds on every provider.
/// </para>
/// </summary>
public sealed class LocationAggregatorConcurrencyTests
{
    /// <summary>
    /// Options with a timeout no loaded machine can trip.
    /// <para>
    /// These four tests are about <em>serialisation</em>, <em>fault isolation</em> and
    /// <em>narrowing</em>. None of them is about the counter budget — but every one of them ran
    /// against <c>new LocationSpatialOptions()</c>, whose default is 1,500ms per counter, while the
    /// fakes below <c>await Task.Delay(5)</c>.
    /// </para>
    /// <para>
    /// Five milliseconds against fifteen hundred looks like an enormous margin, and it is not one
    /// when the thread pool is starved. <c>Task.Delay</c> resumes on a pool thread, and on a
    /// machine with every core busy — a full solution build, say — that resumption can be delayed
    /// by orders of magnitude. The budget then fires, the counter is dropped, and
    /// <c>One_failing_counter_costs_its_own_key_and_nothing_else</c> sees two keys where it
    /// expected two *different* keys.
    /// </para>
    /// <para>
    /// Observed exactly once, on 19 August 2026, in a full-suite run concurrent with a Gradle
    /// build: <b>4,237 passed, 1 failed</b>, and the same test passed 4/4 in isolation moments
    /// later. A test that only passes on a quiet machine is a test that will eventually fail in
    /// CI and be re-run rather than read.
    /// </para>
    /// <para>
    /// So the budget is raised to a minute here. That does not weaken anything: no assertion in
    /// this file depends on a counter timing out, and a genuine deadlock still fails the test by
    /// hanging. The timeout's own behaviour belongs in a test that sets it deliberately.
    /// </para>
    /// </summary>
    private static IOptions<LocationSpatialOptions> UntimedOptions() =>
        Options.Create(new LocationSpatialOptions
        {
            ResourceCounterTimeoutMilliseconds = 60_000,
            NearbyProviderTimeoutMilliseconds = 60_000,
        });

    [Fact]
    public async Task Resource_counters_are_never_asked_two_at_a_time()
    {
        var watch = new OverlapWatch();

        var aggregator = new ResourceCountAggregator(
            new LocationResourceCounterRegistry(
            [
                new WatchingCounter("housing", watch),
                new WatchingCounter("jobs", watch),
                new WatchingCounter("marketplace", watch),
                new WatchingCounter("events", watch)
            ]),
            UntimedOptions(),
            NullLogger<ResourceCountAggregator>.Instance);

        var counts = await aggregator.CountInAreaAsync(
            AreaId.New(), HierarchyPath.ForRoot("GB"));

        counts.Should().HaveCount(
            4, "every counter must be asked, and an omitted key means it could not be");

        watch.MaximumConcurrent.Should().Be(
            1,
            "the contributors share one DbContext, and EF Core refuses a second operation on a "
            + "context while the first is running");
    }

    /// <summary>
    /// The map grid's point listing (14 Oct 2026) runs through the same sequential collector, so
    /// it inherits both properties: one contributor at a time, and a failure costs only its key.
    /// </summary>
    [Fact]
    public async Task Map_points_are_listed_one_counter_at_a_time_and_a_failure_costs_its_own_key()
    {
        var watch = new OverlapWatch();

        var aggregator = new ResourceCountAggregator(
            new LocationResourceCounterRegistry(
            [
                new WatchingCounter("housing", watch),
                new ThrowingCounter("jobs"),
                new WatchingCounter("marketplace", watch),
                new WatchingCounter("events", watch)
            ]),
            UntimedOptions(),
            NullLogger<ResourceCountAggregator>.Instance);

        var points = await aggregator.ListPointsAsync(
            new MapPointQuery { South = -4.2, West = 39.5, North = -3.9, East = 39.8, Limit = 100 },
            resourceTypes: null);

        points.Keys.Should().BeEquivalentTo(["housing", "marketplace", "events"]);
        watch.MaximumConcurrent.Should().Be(1);
    }

    [Fact]
    public async Task Nearby_providers_are_never_asked_two_at_a_time()
    {
        var watch = new OverlapWatch();

        var aggregator = new NearbyAggregator(
            new NearbyResourceProviderRegistry(
            [
                new WatchingProvider("housing", watch),
                new WatchingProvider("jobs", watch),
                new WatchingProvider("marketplace", watch),
                new WatchingProvider("events", watch)
            ]),
            UntimedOptions(),
            NullLogger<NearbyAggregator>.Instance);

        var aggregate = await aggregator.AggregateAsync(Request());

        aggregate.Groups.Should().HaveCount(4);
        aggregate.UnavailableResourceTypes.Should().BeEmpty();
        watch.MaximumConcurrent.Should().Be(1);
    }

    /// <summary>
    /// A contributor that throws still costs only its own group — the isolation the aggregate
    /// endpoint exists for, unchanged by the serialisation above.
    /// </summary>
    [Fact]
    public async Task One_failing_counter_costs_its_own_key_and_nothing_else()
    {
        var watch = new OverlapWatch();

        var aggregator = new ResourceCountAggregator(
            new LocationResourceCounterRegistry(
            [
                new WatchingCounter("housing", watch),
                new ThrowingCounter("jobs"),
                new WatchingCounter("marketplace", watch)
            ]),
            UntimedOptions(),
            NullLogger<ResourceCountAggregator>.Instance);

        var counts = await aggregator.CountInAreaAsync(
            AreaId.New(), HierarchyPath.ForRoot("GB"));

        counts.Keys.Should().BeEquivalentTo(["housing", "marketplace"]);
    }

    /// <summary>The narrowing a map layer relies on: ask one vertical, pay for one query.</summary>
    [Fact]
    public async Task A_narrowed_count_asks_only_the_named_counters()
    {
        var watch = new OverlapWatch();

        var aggregator = new ResourceCountAggregator(
            new LocationResourceCounterRegistry(
            [
                new WatchingCounter("housing", watch),
                new WatchingCounter("jobs", watch)
            ]),
            UntimedOptions(),
            NullLogger<ResourceCountAggregator>.Instance);

        var counts = await aggregator.CountInAreaAsync(
            AreaId.New(), HierarchyPath.ForRoot("GB"), ["housing"]);

        counts.Keys.Should().BeEquivalentTo(["housing"]);
        watch.Entries.Should().Be(1);
    }

    private static NearbyResourceRequest Request()
    {
        var search = NearbySearch.Create(
            GeoCoordinate.Create(51.4742d, -0.0691d),
            SearchRadius.FromKilometers(5d).Value,
            new HaversineDistanceCalculator(),
            levels: [AdministrativeLevel.Neighborhood]);

        return new NearbyResourceRequest
        {
            Search = search.Value,
            CountryCode = CountryCode.Create("GB"),
            Limit = 10
        };
    }

    /// <summary>Records how many contributors were inside their call at the same moment.</summary>
    private sealed class OverlapWatch
    {
        private int _current;

        public int MaximumConcurrent { get; private set; }

        public int Entries { get; private set; }

        public async Task ObserveAsync()
        {
            var now = Interlocked.Increment(ref _current);

            lock (this)
            {
                Entries++;
                MaximumConcurrent = Math.Max(MaximumConcurrent, now);
            }

            // A real suspension. Without one the fake would complete synchronously and the
            // overlap the shared DbContext actually suffers could never be observed — which is
            // exactly why the SQLite integration harness cannot see this defect.
            await Task.Delay(5);

            Interlocked.Decrement(ref _current);
        }
    }

    private sealed class WatchingCounter : ILocationResourceCounter
    {
        private readonly OverlapWatch _watch;

        public WatchingCounter(string resourceType, OverlapWatch watch)
        {
            ResourceType = resourceType;
            _watch = watch;
        }

        public string ResourceType { get; }

        public async Task<int> CountInAreaAsync(
            AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default)
        {
            await _watch.ObserveAsync();
            return 1;
        }

        public async Task<int> CountInCountryAsync(
            CountryCode countryCode, CancellationToken cancellationToken = default)
        {
            await _watch.ObserveAsync();
            return 1;
        }

        public async Task<MapPointPage> ListPointsAsync(
            MapPointQuery query, CancellationToken cancellationToken = default)
        {
            await _watch.ObserveAsync();
            return new MapPointPage([new MapPoint(query.South, query.West)], false);
        }
    }

    private sealed class ThrowingCounter : ILocationResourceCounter
    {
        public ThrowingCounter(string resourceType) => ResourceType = resourceType;

        public string ResourceType { get; }

        public Task<int> CountInAreaAsync(
            AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "A second operation was started on this context instance.");

        public Task<int> CountInCountryAsync(
            CountryCode countryCode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "A second operation was started on this context instance.");

        public Task<MapPointPage> ListPointsAsync(
            MapPointQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "A second operation was started on this context instance.");
    }

    private sealed class WatchingProvider : INearbyResourceProvider
    {
        private readonly OverlapWatch _watch;

        public WatchingProvider(string resourceType, OverlapWatch watch)
        {
            ResourceType = resourceType;
            _watch = watch;
        }

        public string ResourceType { get; }

        public async Task<NearbyResourceGroup> FindNearbyAsync(
            NearbyResourceRequest request, CancellationToken cancellationToken = default)
        {
            await _watch.ObserveAsync();

            return new NearbyResourceGroup { ResourceType = ResourceType, Items = [] };
        }
    }
}
