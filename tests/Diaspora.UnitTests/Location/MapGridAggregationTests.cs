using Diaspora.Modules.Location.Application.Services;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diaspora.UnitTests.Location;

/// <summary>
/// Phase 2B of retiring the place catalogue (14 Oct 2026): map clusters and the heat layer are
/// square grid cells over each row's own coordinates, not catalogue nodes.
/// <para>
/// Every fixture here is in Kenya, a country the catalogue never held — the case the old
/// node-based cells put nothing on the map for.
/// </para>
/// </summary>
public sealed class MapGridAggregationTests
{
    // Mombasa island and Nyali, a few kilometres apart; Nairobi, 440 km away.
    private static readonly MapPoint MombasaOldTown = new(-4.0610, 39.6770);
    private static readonly MapPoint MombasaFortJesus = new(-4.0627, 39.6795);
    private static readonly MapPoint MombasaMarket = new(-4.0598, 39.6752);
    private static readonly MapPoint Nairobi = new(-1.2864, 36.8172);

    // The southern half of Kenya: Nairobi and Mombasa both inside.
    private const string KenyaSouth = "0,-5,41,36";

    [Fact]
    public async Task Rows_from_a_country_with_no_catalogue_are_bucketed_with_counts_per_type()
    {
        var aggregator = Aggregator(
            new PointCounter("housing", MombasaOldTown, MombasaFortJesus, Nairobi),
            new PointCounter("jobs", MombasaMarket),
            new PointCounter("events"));

        var page = (await aggregator.AggregateAsync(KenyaSouth, "City", null, null)).Value;

        page.Cells.Should().HaveCount(2, "Mombasa's three rows share a cell and Nairobi has its own");

        var mombasa = page.Cells[0];
        mombasa.Total.Should().Be(3);
        mombasa.Counts.Should().BeEquivalentTo(
            new Dictionary<string, int> { ["housing"] = 2, ["jobs"] = 1, ["events"] = 0 },
            "a vertical that answered with nothing here is a zero, not an absent key");
        mombasa.Latitude.Should().BeApproximately(-4.0612, 0.001, "three rows: the mean of them");
        mombasa.Longitude.Should().BeApproximately(39.6772, 0.001);
        mombasa.South.Should().BeLessThanOrEqualTo(-4.0627);
        mombasa.North.Should().BeGreaterThanOrEqualTo(-4.0598);

        var nairobi = page.Cells[1];
        nairobi.Total.Should().Be(1);
        nairobi.Counts["housing"].Should().Be(1);
        nairobi.Latitude.Should().Be(
            (nairobi.South + nairobi.North) / 2,
            "a cell of one row is marked at its centre, never on the row's own pin");
        nairobi.Label.Should().StartWith("Area around");

        page.Truncated.Should().BeFalse();
        page.SuppressedCells.Should().Be(0);
    }

    [Fact]
    public async Task A_vertical_that_could_not_be_asked_is_an_absent_key_in_every_cell()
    {
        var aggregator = Aggregator(
            new PointCounter("housing", MombasaOldTown),
            new FailingCounter("jobs"));

        var page = (await aggregator.AggregateAsync(KenyaSouth, null, null, null)).Value;

        page.Cells.Should().ContainSingle()
            .Which.Counts.Keys.Should().BeEquivalentTo(["housing"]);
    }

    [Fact]
    public async Task Cells_below_the_floor_are_withheld_and_counted()
    {
        var aggregator = Aggregator(
            Options.Create(new LocationSpatialOptions
            {
                MinimumClusterCellCount = 2,
                ResourceCounterTimeoutMilliseconds = 60_000
            }),
            new PointCounter("housing", MombasaOldTown, MombasaFortJesus, Nairobi));

        var page = (await aggregator.AggregateAsync(KenyaSouth, "City", null, null)).Value;

        page.Cells.Should().ContainSingle().Which.Total.Should().Be(2);
        page.SuppressedCells.Should().Be(1);
        page.MinimumCellCount.Should().Be(2);
    }

    [Fact]
    public async Task A_cell_keeps_its_key_and_id_when_the_map_pans()
    {
        var aggregator = Aggregator(new PointCounter("housing", MombasaOldTown));

        var first = (await aggregator.AggregateAsync("-3.5,-4.5,40.2,39.2", null, null, null)).Value;
        var panned = (await aggregator.AggregateAsync("-3.6,-4.6,40.1,39.1", null, null, null)).Value;

        first.CellSizeDegrees.Should().Be(panned.CellSizeDegrees);
        panned.Cells.Single().Key.Should().Be(first.Cells.Single().Key);
        panned.Cells.Single().Id.Should().Be(first.Cells.Single().Id);
    }

    [Fact]
    public void The_cell_side_follows_the_viewport_and_the_grid_never_exceeds_the_cap()
    {
        var country = BoundingBox.Create(north: 5, south: -5, east: 42, west: 33);
        var town = BoundingBox.Create(north: -4.0, south: -4.1, east: 39.75, west: 39.6);

        var coarse = MapCellAggregator.ChooseExponent(country, AdministrativeLevel.City);
        var fine = MapCellAggregator.ChooseExponent(country, AdministrativeLevel.Neighborhood);
        var street = MapCellAggregator.ChooseExponent(town, AdministrativeLevel.Neighborhood);

        coarse.Should().BeGreaterThan(fine);
        fine.Should().BeGreaterThan(street);

        // A one-kilometre cell over a whole country would be thousands of cells: it is coarsened.
        var forced = MapCellAggregator.ChooseExponent(country, AdministrativeLevel.City, cellSizeKm: 1);
        var side = Math.Pow(2, forced);
        ((Math.Ceiling(10 / side) + 1) * (Math.Ceiling(9 / side) + 1))
            .Should().BeLessThanOrEqualTo(MapCellAggregator.MaxCells);
    }

    [Fact]
    public async Task A_vertical_at_its_read_cap_marks_the_page_truncated()
    {
        var aggregator = Aggregator(new PointCounter("housing", truncated: true, MombasaOldTown));

        var page = (await aggregator.AggregateAsync(KenyaSouth, null, null, null)).Value;

        page.Truncated.Should().BeTrue("the totals are lower bounds and the map must say so");
    }

    [Fact]
    public void The_point_reader_drops_unpinned_rows_and_rows_outside_the_box()
    {
        var query = new MapPointQuery { South = -5, West = 36, North = 0, East = 41, Limit = 10 };

        var page = MapPointReader.FromList<(double? Lat, double? Lng)>(
            query,
            [(-4.06, 39.67), (null, null), (-4.06, null), (51.5, -0.12)],
            row => (row.Lat, row.Lng));

        page.Points.Should().ContainSingle().Which.Should().Be(new MapPoint(-4.06, 39.67));
        page.IsTruncated.Should().BeFalse();
    }

    [Fact]
    public void The_point_reader_honours_a_viewport_across_the_antimeridian()
    {
        var query = new MapPointQuery { South = -20, West = 170, North = -10, East = -170, Limit = 10 };

        query.Contains(-17.7, 178.0).Should().BeTrue("Fiji, west of the line");
        query.Contains(-14.3, -178.1).Should().BeTrue("Wallis and Futuna, east of it");
        query.Contains(-17.7, 0).Should().BeFalse();
    }

    private static MapCellAggregator Aggregator(params ILocationResourceCounter[] counters) =>
        Aggregator(
            Options.Create(new LocationSpatialOptions { ResourceCounterTimeoutMilliseconds = 60_000 }),
            counters);

    private static MapCellAggregator Aggregator(
        IOptions<LocationSpatialOptions> options, params ILocationResourceCounter[] counters) =>
        new(
            new ResourceCountAggregator(
                new LocationResourceCounterRegistry(counters),
                options,
                NullLogger<ResourceCountAggregator>.Instance),
            options);

    private sealed class PointCounter : ILocationResourceCounter
    {
        private readonly MapPoint[] _points;
        private readonly bool _truncated;

        public PointCounter(string resourceType, params MapPoint[] points)
            : this(resourceType, false, points)
        {
        }

        public PointCounter(string resourceType, bool truncated, params MapPoint[] points)
        {
            ResourceType = resourceType;
            _points = points;
            _truncated = truncated;
        }

        public string ResourceType { get; }

        public Task<int> CountInAreaAsync(
            AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> CountInCountryAsync(
            CountryCode countryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<MapPointPage> ListPointsAsync(
            MapPointQuery query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new MapPointPage(
                [.. _points.Where(point => query.Contains(point.Latitude, point.Longitude))],
                _truncated));
    }

    private sealed class FailingCounter : ILocationResourceCounter
    {
        public FailingCounter(string resourceType) => ResourceType = resourceType;

        public string ResourceType { get; }

        public Task<int> CountInAreaAsync(
            AreaId areaId, HierarchyPath path, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<int> CountInCountryAsync(
            CountryCode countryCode, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<MapPointPage> ListPointsAsync(
            MapPointQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Jobs did not answer.");
    }
}
