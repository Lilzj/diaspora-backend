using System.Diagnostics;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Countries;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Volume, and what stays bounded at it.
/// <para>
/// <b>Correctness tests that happen to be large.</b> The timings they print go in the performance
/// report; none asserts on wall-clock time, because a loaded build agent would fail a clock
/// assertion for reasons that have nothing to do with the code, and a suite that goes red for that
/// stops being read. It is the platform's established shape and this is its sibling.
/// </para>
/// <para>
/// What they do assert is what only appears at volume, and for a geographic module that is one
/// property above all others: <b>every read is bounded by something the server chose rather than by
/// something the caller or the data chose.</b> A density layer whose cost grows with how many
/// places are in view, or a score whose comparison population grows with how many neighbourhoods a
/// country has, is a full-table scan that behaves perfectly in a launch country and falls over in
/// the first large one.
/// </para>
/// <para>
/// <b>Its own country, throughout.</b> The class shares one database with the rest of the suite, so
/// a test asserting on a global count would pass or fail depending on which other tests ran first.
/// </para>
/// </summary>
public sealed class GeographicIntelligenceScaleTests : IdentityTestBase
{
    private const string Root = "/api/v1/geo";

    /// <summary>Kenya. Nothing else in the suite seeds neighbourhoods here.</summary>
    private const string ScaleCountry = "KE";

    /// <summary>Comfortably past the fifty B11's directory returns and the twenty a viewport holds.</summary>
    private const int Neighborhoods = 90;

    /// <summary>How many of them are given published properties, and how many each gets.</summary>
    private const int PopulatedCells = 6;

    /// <summary>
    /// A rectangle around the six populated neighbourhoods and no others.
    /// <para>
    /// Areas are seeded a hundredth of a degree apart from (-1.0, 36.0), so indices 0 to 5 sit
    /// between -1.00 and -0.95 latitude. Tight, because the cell cap would otherwise decide which
    /// twenty of ninety places answer and the assertion would depend on the provider's ordering.
    /// </para>
    /// </summary>
    private const string PopulatedViewport = "north=-0.945&south=-1.005&east=36.055&west=35.995";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITestOutputHelper _output;

    public GeographicIntelligenceScaleTests(
        CustomWebApplicationFactory factory, ITestOutputHelper output) : base(factory) =>
        _output = output;

    /// <summary>
    /// <b>The comparison population is capped by the server, not by the country.</b> Ninety
    /// profiled neighbourhoods produce a sample size of fifty, because B11's directory caps its
    /// answer there — so the cost of a score does not grow with how large a country gets.
    /// </summary>
    [Fact]
    public async Task A_score_in_a_large_country_ranks_against_a_bounded_population()
    {
        var slugs = await SeedAsync();

        await AuthenticateMemberAsync();

        var stopwatch = Stopwatch.StartNew();
        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/{slugs[0]}/scores");
        stopwatch.Stop();

        var housing = score.Dimensions.Single(dimension => dimension.Id == "housing");

        housing.Freshness.SampleSize.Should().Be(
            50,
            "the directory caps its answer at fifty, so a country with ninety profiled "
            + "neighbourhoods costs the same as one with fifty-one");

        _output.WriteLine(
            $"Composed a score over {Neighborhoods} profiled neighbourhoods in "
            + $"{stopwatch.ElapsedMilliseconds} ms "
            + $"(sample size {housing.Freshness.SampleSize}, "
            + $"{score.Dimensions.Count} dimensions served, "
            + $"{score.UnavailableDimensions.Count} explained).");
    }

    /// <summary>
    /// <b>A dense viewport is truncated and says so.</b> The cell cap is twenty; ninety places in
    /// one rectangle must produce twenty cells and <c>truncated: true</c>, never twenty cells
    /// silently. A truncated map that says nothing reads as a complete one, and a member would
    /// conclude a district was empty.
    /// </summary>
    [Fact]
    public async Task A_dense_viewport_is_capped_and_declares_that_it_was()
    {
        await SeedAsync();

        await AuthenticateMemberAsync();

        var stopwatch = Stopwatch.StartNew();

        var layer = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=housing-availability"
            + $"&north=1.5&south=-1.5&east=38.5&west=35.5&countryCode={ScaleCountry}");

        stopwatch.Stop();

        layer.Points.Count.Should().BeLessThanOrEqualTo(
            20, "the cell cap bounds the response regardless of how many places are in view");

        layer.Truncated.Should().BeTrue(
            $"{Neighborhoods} places are in the rectangle and only 20 are returned");

        _output.WriteLine(
            $"Aggregated a viewport holding {Neighborhoods} places into {layer.Points.Count} cells "
            + $"in {stopwatch.ElapsedMilliseconds} ms "
            + $"({layer.SuppressedCells} suppressed, truncated: {layer.Truncated}).");
    }

    /// <summary>
    /// <b>The normalisation, over cells that actually hold something.</b> The first version of this
    /// suite asserted the layer's shape against a viewport whose every cell was empty and suppressed
    /// — it passed, and proved nothing about the arithmetic that turns counts into a gradient. This
    /// seeds six neighbourhoods with one to six published properties and asserts the whole scale:
    /// the busiest cell is 1, the quietest is 0, and every raw value is that vertical's real count.
    /// </summary>
    [Fact]
    public async Task A_populated_layer_normalises_real_counts_across_the_viewport()
    {
        await SeedAsync();
        await SeedListingsAsync();

        await AuthenticateMemberAsync();

        var layer = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=housing-availability"
            + $"&{PopulatedViewport}&countryCode={ScaleCountry}");

        layer.Points.Should().HaveCount(
            PopulatedCells, "six neighbourhoods in this rectangle hold published properties");

        layer.Unit.Should().Be("listings");
        layer.MinValue.Should().Be(1);
        layer.MaxValue.Should().Be(PopulatedCells);

        layer.Points.Select(point => point.RawValue).Should().BeEquivalentTo(
            Enumerable.Range(1, PopulatedCells),
            "each cell reports the housing count actually filed against it");

        layer.Points.Single(point => point.RawValue == PopulatedCells)
            .Intensity.Should().Be(1d, "the busiest cell anchors the top of the gradient");

        layer.Points.Single(point => point.RawValue == 1)
            .Intensity.Should().Be(0d, "the quietest anchors the bottom");

        layer.SuppressedCells.Should().Be(
            0, "every cell in this rectangle clears the floor of one");

        _output.WriteLine(
            $"Normalised {layer.Points.Count} populated cells over "
            + $"{layer.MinValue}–{layer.MaxValue} listings.");
    }

    /// <summary>
    /// <b>The cost of a layer does not grow with how many verticals exist.</b> A layer names one
    /// resource type, so the counters run once per cell rather than once per cell per vertical —
    /// twenty queries instead of a hundred and forty. A jobs layer over the same rectangle that
    /// answered with the housing counts would be one that stopped narrowing.
    /// </summary>
    [Fact]
    public async Task A_layer_counts_one_vertical_rather_than_every_vertical()
    {
        await SeedAsync();
        await SeedListingsAsync();

        await AuthenticateMemberAsync();

        var jobs = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=jobs&{PopulatedViewport}&countryCode={ScaleCountry}");

        jobs.Unit.Should().Be("jobs");

        jobs.Points.Should().BeEmpty(
            "no job is filed in these neighbourhoods, and the housing filed there must not leak "
            + "into a jobs layer");

        jobs.SuppressedCells.Should().Be(
            PopulatedCells, "the cells exist and are withheld for holding no jobs");
    }

    /// <summary>
    /// A comparison of the maximum number of subjects over a large country is still one bounded
    /// read per subject. The ceiling is what keeps it that way.
    /// </summary>
    [Fact]
    public async Task A_full_comparison_over_a_large_country_stays_bounded()
    {
        var slugs = await SeedAsync();

        await AuthenticateMemberAsync();

        var stopwatch = Stopwatch.StartNew();

        var page = await GetAsync<List<ScoreDto>>(
            $"{Root}/neighborhoods/compare?ids={string.Join(',', slugs.Take(5))}");

        stopwatch.Stop();

        page.Should().HaveCount(5);

        _output.WriteLine(
            $"Compared 5 neighbourhoods across {Neighborhoods} profiled places in "
            + $"{stopwatch.ElapsedMilliseconds} ms.");
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ninety neighbourhoods in one country, each with a profile, a listing count and a centre
    /// inside one rectangle. Idempotent, because the class shares its database with its siblings.
    /// </summary>
    private async Task<List<string>> SeedAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        var countries = scope.ServiceProvider.GetRequiredService<ICountryRepository>();

        var country = await countries.GetByCodeAsync(CountryCode.TryCreate(ScaleCountry).Value);
        country.Should().NotBeNull($"B4 seeds {ScaleCountry} in its launch catalogue");

        var slugs = Enumerable.Range(0, Neighborhoods)
            .Select(index => $"scale-{index:D3}")
            .ToList();

        var existing = (await dbContext.Set<AdministrativeArea>()
                .Where(node => node.Level == AdministrativeLevel.Neighborhood)
                .ToListAsync())
            .Where(node => node.Slug.Value.StartsWith("scale-", StringComparison.Ordinal))
            .ToDictionary(node => node.Slug.Value, node => node, StringComparer.Ordinal);

        if (existing.Count >= Neighborhoods)
        {
            return slugs;
        }

        var now = DateTimeOffset.UtcNow;

        for (var index = 0; index < Neighborhoods; index++)
        {
            var slug = slugs[index];

            if (existing.ContainsKey(slug))
            {
                continue;
            }

            var area = AdministrativeArea.CreateUnderCountry(
                country!,
                AdministrativeLevel.Neighborhood,
                PlaceName.Create($"Scale {index:D3}"),
                LocationSlug.TryCreate(slug).Value).Value;

            // Spread across one degree so every one of them lands inside the test's rectangle.
            area.SetCenter(GeoCoordinate.Create(
                -1.0d + (index * 0.01d), 36.0d + (index * 0.01d)));

            dbContext.Set<AdministrativeArea>().Add(area);

            var profile = NeighborhoodProfile.Create(
                NeighborhoodProfileId.New(),
                area.Id.Value,
                ScaleCountry,
                area.Name.Value,
                slug,
                now).Value;

            // A listing count, because B11's directory only returns neighbourhoods that have one —
            // a profile with none would be absent from the very population being measured.
            profile.SetListingCount(index + 1);

            dbContext.Set<NeighborhoodProfile>().Add(profile);
        }

        await dbContext.SaveChangesAsync();

        return slugs;
    }

    /// <summary>
    /// Gives the first six seeded neighbourhoods one to six published properties each.
    /// <para>
    /// Real <c>HousingListing</c> rows, filed against B4's area path, because that is what the
    /// housing counter counts. The denormalised <c>ListingCount</c> on a profile is Housing's own
    /// field and is invisible to the counter registry — seeding only that would have produced a
    /// layer of empty cells, which is exactly what the first version of this suite measured.
    /// </para>
    /// </summary>
    private async Task SeedListingsAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        if (await dbContext.Set<HousingListing>()
                .AnyAsync(listing => listing.Slug.Value.StartsWith("geo-scale-")))
        {
            return;
        }

        var areas = (await dbContext.Set<AdministrativeArea>()
                .AsNoTracking()
                .Where(node => node.Level == AdministrativeLevel.Neighborhood)
                .ToListAsync())
            .Where(node => node.Slug.Value.StartsWith("scale-", StringComparison.Ordinal))
            .OrderBy(node => node.Slug.Value, StringComparer.Ordinal)
            .Take(PopulatedCells)
            .ToList();

        areas.Should().HaveCount(PopulatedCells);

        var landlordId = LandlordProfileId.New();
        var ownerId = UserId.New();
        var now = DateTimeOffset.UtcNow;

        for (var cell = 0; cell < areas.Count; cell++)
        {
            for (var copy = 0; copy <= cell; copy++)
            {
                var listing = HousingListing.Draft(
                    HousingListingId.New(),
                    landlordId,
                    ownerId,
                    HousingTitle.Create($"Scale {cell}-{copy}").Value,
                    HousingSlug.Create($"geo-scale-{cell}-{copy}"),
                    HousingDescription.Create(
                        "A seeded property for the geographic scale suite.").Value,
                    "apartment",
                    HousingPrice.Create(900m, "KES", RentPeriod.PerMonth, null).Value,
                    HousingCapacity.Create(1, 1m, maximumOccupants: 2).Value,
                    HousingAvailability.Create(null, totalUnits: 1).Value,
                    HousingLocationContext.Create(
                        ScaleCountry,
                        regionName: null,
                        cityName: null,
                        areas[cell].Name.Value,
                        areas[cell].Id.Value,
                        areas[cell].Path.Value).Value,
                    PropertyAddress.Create(
                        latitude: areas[cell].Center!.Latitude,
                        longitude: areas[cell].Center!.Longitude).Value,
                    StayPolicy.Create(
                        acceptsShortStay: false,
                        acceptsLongStay: true,
                        isSharedAccommodation: false).Value,
                    now).Value;

                listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, now);

                // Asserted rather than ignored: a seed that silently failed to publish would make
                // the normalisation assertions pass against an empty layer, which is the failure
                // mode this method exists to remove.
                listing.Publish(now).IsSuccess.Should().BeTrue();

                dbContext.Set<HousingListing>().Add(listing);
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task AuthenticateMemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task<T> GetAsync<T>(string route)
    {
        var response = await HttpClient.GetAsync(route);
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded but returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record ScoreDto(
        string NeighborhoodId,
        List<DimensionDto> Dimensions,
        List<UnavailableDto> UnavailableDimensions);

    private sealed record DimensionDto(string Id, int Score, FreshnessDto Freshness);

    private sealed record UnavailableDto(string Id, string Reason);

    private sealed record FreshnessDto(string SourceName, string? Methodology, int? SampleSize);

    private sealed record HeatDto(
        string Layer,
        string Unit,
        List<HeatPointDto> Points,
        int MinValue,
        int MaxValue,
        int MinimumCellCount,
        int SuppressedCells,
        bool Truncated);

    private sealed record HeatPointDto(
        double Latitude, double Longitude, double Intensity, int RawValue);
}
