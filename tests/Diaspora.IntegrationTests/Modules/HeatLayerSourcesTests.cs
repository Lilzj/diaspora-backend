using System.Net;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The three heat layers that were refused until 21 Oct 2026 — housing price, cost of living and
/// safety — and the time range every layer now takes, driven over real HTTP against real rows.
/// <para>
/// <b>Every scenario owns its country and its viewport.</b> Rent medians and the cost-of-living
/// baseline are per country, and the database is shared with every other suite in the collection,
/// so a test that seeded homes in Britain would move the numbers <c>CostOfLivingRentIndexTests</c>
/// pins. Each test here seeds a country nothing else lists homes in, at coordinates nothing else
/// uses, and asserts only on the cells inside its own viewport.
/// </para>
/// <para>
/// Homes are placed at two-decimal coordinates on purpose: a stranger is shown a home's position
/// rounded to two decimals, and the layers bucket what a stranger is shown.
/// </para>
/// </summary>
[Collection(AssortedCollection.Name)]
public sealed class HeatLayerSourcesTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/geo/heatmap";

    public HeatLayerSourcesTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Housing price and cost of living ─────────────────────────────────────────────────────────

    /// <summary>
    /// Five homes in one cell get a median; two in another are withheld and listed, not drawn as
    /// zero; the cost-of-living index uses the country's median over all twenty — the thirteen with
    /// no coordinate included — exactly as a neighbourhood's cost of living does.
    /// </summary>
    [Fact]
    public async Task Rent_layers_publish_a_median_and_an_index_per_cell_and_withhold_thin_cells()
    {
        const string Country = "IS";
        var now = DateTimeOffset.UtcNow;

        await SeedHomesAsync(Country, now, (64.14, -21.94), 1000m, 1100m, 1200m, 1300m, 1400m);
        await SeedHomesAsync(Country, now, (64.30, -21.50), 500m, 600m);
        await SeedHomesAsync(Country, now, null, [.. Enumerable.Repeat(800m, 13)]);

        await SignInAsync();

        const string Viewport = "north=64.35&south=64.10&east=-21.45&west=-22.00&level=Neighborhood&countryCode=IS";

        var price = await GetAsync($"{Root}?layer=housing-price&{Viewport}");

        price.IsAvailable.Should().BeTrue();
        price.Source.Should().Be("listings");
        price.Currency.Should().Be("ISK");
        price.Freshness.SourceName.Should().Be("Diaspora listings");

        var cell = price.Points.Should().ContainSingle().Subject;
        cell.RawValue.Should().Be(1200, "the median of 1,000 to 1,400");
        cell.SampleSize.Should().Be(5);
        cell.AreaName.Should().StartWith("Area around", "a cell is a grid square, never a named place");

        var withheld = price.WithheldCells.Should().ContainSingle().Subject;
        withheld.Reason.Should().Be("TooFewHomes");
        withheld.Detail.Should().Contain("5");
        price.SuppressedCells.Should().Be(1);
        price.MinimumCellCount.Should().Be(5);

        var cost = await GetAsync($"{Root}?layer=cost-of-living&{Viewport}");

        cost.IsAvailable.Should().BeTrue();
        cost.MinimumCountrySample.Should().Be(20);
        cost.Points.Should().ContainSingle().Which.RawValue.Should().Be(
            150, "1,200 against the country's median of 800 over all twenty homes");
        cost.WithheldCells.Should().ContainSingle().Which.Reason.Should().Be("TooFewHomes");
        cost.Freshness.Methodology.Should().Contain("20");
        cost.Freshness.Limitations.Should().Contain(line => line.StartsWith("Housing only"));
    }

    /// <summary>
    /// Below the country minimum there is no baseline, so no cell is indexed — every cell that holds
    /// homes is withheld with that reason — while the price layer, which needs no baseline, still
    /// publishes the median.
    /// </summary>
    [Fact]
    public async Task The_cost_of_living_layer_withholds_every_cell_until_the_country_has_a_baseline()
    {
        const string Country = "GL";
        var now = DateTimeOffset.UtcNow;

        await SeedHomesAsync(Country, now, (64.18, -51.72), 900m, 950m, 1000m, 1050m, 1100m);

        await SignInAsync();

        const string Viewport = "north=64.25&south=64.10&east=-51.60&west=-51.85&level=Neighborhood&countryCode=GL";

        var cost = await GetAsync($"{Root}?layer=cost-of-living&{Viewport}");

        cost.IsAvailable.Should().BeFalse();
        cost.UnavailableReason.Should().Be("CountrySampleTooSmall");
        cost.Points.Should().BeEmpty();
        cost.WithheldCells.Should().ContainSingle().Which.Reason.Should().Be("CountrySampleTooSmall");
        cost.WithheldCells[0].Detail.Should().Contain("at least 20");

        var price = await GetAsync($"{Root}?layer=housing-price&{Viewport}");

        price.IsAvailable.Should().BeTrue();
        price.Points.Should().ContainSingle().Which.RawValue.Should().Be(1000);
    }

    /// <summary>A rent layer compares in one country's currency, so it needs the country.</summary>
    [Theory]
    [InlineData("housing-price")]
    [InlineData("cost-of-living")]
    public async Task A_rent_layer_without_a_country_is_refused(string layer)
    {
        await SignInAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}?layer={layer}&north=64.35&south=64.10&east=-21.45&west=-22.00");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("geo.heat.country_required");
    }

    // ── Time range ───────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The range filters on each home's own publication time, on the count layer and on the rent
    /// layer alike — and with no range, nothing is filtered, as before.
    /// </summary>
    [Fact]
    public async Task A_time_range_counts_only_what_was_published_inside_it()
    {
        const string Country = "FO";
        var now = DateTimeOffset.UtcNow;

        await SeedHomesAsync(Country, now.AddDays(-2), (62.01, -6.77), 2000m, 2100m, 2200m, 2300m, 2400m);
        await SeedHomesAsync(Country, now.AddDays(-40), (62.01, -6.77), 900m, 1000m, 1100m, 1200m, 1300m);

        await SignInAsync();

        const string Viewport = "north=62.05&south=61.95&east=-6.70&west=-6.85&level=Neighborhood&countryCode=FO";

        var week = await GetAsync($"{Root}?layer=housing-availability&{Viewport}&timeRange=7d");
        var quarter = await GetAsync($"{Root}?layer=housing-availability&{Viewport}&timeRange=90d");
        var always = await GetAsync($"{Root}?layer=housing-availability&{Viewport}");

        week.Points.Sum(point => point.RawValue).Should().Be(5, "only the five published two days ago");
        week.TimeRange.Should().Be("7d");
        week.TimeBasis.Should().Be("published");
        week.From.Should().BeCloseTo(now.AddDays(-7), TimeSpan.FromMinutes(5));
        week.Freshness.Limitations.Should().Contain(line => line.Contains("last 7 days"));

        quarter.Points.Sum(point => point.RawValue).Should().Be(10);
        always.Points.Sum(point => point.RawValue).Should().Be(10);
        always.TimeRange.Should().BeNull();
        always.From.Should().BeNull();

        var recentPrice = await GetAsync($"{Root}?layer=housing-price&{Viewport}&timeRange=30d");
        recentPrice.Points.Should().ContainSingle().Which.RawValue.Should().Be(2200);

        var allPrice = await GetAsync($"{Root}?layer=housing-price&{Viewport}");
        allPrice.Points.Should().ContainSingle().Which.RawValue.Should().Be(1650, "the median of all ten");
    }

    /// <summary>
    /// An unknown range is a 400, never "no filter": a layer labelled "last 24 hours" that was not
    /// filtered is a false statement on a map.
    /// </summary>
    [Theory]
    [InlineData("1y")]
    [InlineData("7")]
    [InlineData("forever")]
    public async Task An_unknown_time_range_is_refused(string range)
    {
        await SignInAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}?layer=jobs&north=51.49&south=51.46&east=-0.04&west=-0.10&timeRange={range}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("geo.heat.time_range_invalid");
    }

    [Theory]
    [InlineData("24h")]
    [InlineData("7D")]
    [InlineData("30d")]
    [InlineData("90d")]
    public async Task Every_range_the_client_offers_is_accepted(string range)
    {
        await SignInAsync();

        var layer = await GetAsync(
            $"{Root}?layer=events&north=51.49&south=51.46&east=-0.04&west=-0.10&timeRange={range}");

        layer.TimeRange.Should().Be(range.ToLowerInvariant());
        layer.TimeBasis.Should().Be("starts", "an event is placed in time by when it starts");
    }

    // ── Safety ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Community reports are counted per cell at their own pins: corroborated Police and Emergency
    /// alerts only, inside the window — the safety read's 90 days by default — never a score.
    /// </summary>
    [Fact]
    public async Task The_safety_layer_counts_corroborated_community_reports_per_cell_and_honours_the_range()
    {
        var now = DateTimeOffset.UtcNow;
        var pin = (Latitude: 65.684, Longitude: -18.091);

        await SeedReportAsync(pin, PostCategory.Police, now.AddDays(-2), corroborated: true);
        await SeedReportAsync(pin, PostCategory.Police, now.AddDays(-3), corroborated: true);
        await SeedReportAsync(pin, PostCategory.Emergency, now.AddDays(-5), corroborated: true);
        await SeedReportAsync(pin, PostCategory.Police, now.AddDays(-20), corroborated: true);

        // Not counted, each for its own reason.
        await SeedReportAsync(pin, PostCategory.Police, now.AddDays(-120), corroborated: true);
        await SeedReportAsync(pin, PostCategory.Police, now.AddDays(-1), corroborated: false);
        await SeedReportAsync(pin, PostCategory.Traffic, now.AddDays(-1), corroborated: true);

        await SignInAsync();

        const string Viewport = "north=65.75&south=65.60&east=-18.00&west=-18.20&level=Neighborhood";

        var standard = await GetAsync($"{Root}?layer=safety&{Viewport}");

        standard.IsAvailable.Should().BeTrue();
        standard.Source.Should().Be("community-reports");
        standard.Unit.Should().Be("reports");
        standard.Freshness.SourceName.Should().Be("Diaspora community reports");
        standard.Freshness.Limitations.Should().Contain(line => line.StartsWith("Diaspora does not publish a safety score"));
        standard.From.Should().BeCloseTo(now.AddDays(-90), TimeSpan.FromMinutes(5));

        var cell = standard.Points.Should().ContainSingle().Subject;
        cell.RawValue.Should().Be(4);
        cell.Categories.Should().BeEquivalentTo(new Dictionary<string, int> { ["Police"] = 3, ["Emergency"] = 1 });

        var week = await GetAsync($"{Root}?layer=safety&{Viewport}&timeRange=7d&source=community-reports");

        week.Points.Should().ContainSingle().Which.RawValue.Should().Be(3);
    }

    /// <summary>
    /// The police source is a separate layer, never blended in. It is off in this host, so it must
    /// say so — with the licence it would carry — rather than draw an empty map; a country no source
    /// covers is told nobody publishes the figure, and carries no credit for data nobody sent.
    /// </summary>
    [Fact]
    public async Task The_police_source_is_its_own_layer_and_says_why_it_is_empty()
    {
        await SignInAsync();

        var british = await GetAsync(
            $"{Root}?layer=safety&source=police-recorded-crime&north=51.49&south=51.46&east=-0.04&west=-0.10&countryCode=GB");

        british.IsAvailable.Should().BeFalse();
        british.UnavailableReason.Should().Be("Disabled");
        british.Source.Should().Be("police-recorded-crime");
        british.Points.Should().BeEmpty();
        british.Attribution.Should().Contain("Open Government Licence");

        var mauritian = await GetAsync(
            $"{Root}?layer=safety&source=police-recorded-crime&north=-20.20&south=-20.30&east=57.55&west=57.45&countryCode=MU");

        mauritian.IsAvailable.Should().BeFalse();
        mauritian.UnavailableReason.Should().Be("NotPublished");
        mauritian.Attribution.Should().BeNull();
    }

    [Fact]
    public async Task The_police_layer_refuses_a_viewport_the_source_cannot_read_in_one_piece()
    {
        await SignInAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}?layer=safety&source=police-recorded-crime&north=52.0&south=51.0&east=0.5&west=-0.5&countryCode=GB");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("geo.bounds.too_large");
    }

    [Theory]
    [InlineData("safety", "both")]
    [InlineData("safety", "crime-score")]
    [InlineData("jobs", "community-reports")]
    public async Task A_source_a_layer_does_not_have_is_refused(string layer, string source)
    {
        await SignInAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}?layer={layer}&source={source}&north=51.49&south=51.46&east=-0.04&west=-0.10");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("geo.heat.source_unsupported");
    }

    [Fact]
    public async Task The_capability_report_lists_all_eight_layers()
    {
        await SignInAsync();

        var capabilities = await IdentityApiClient.ReadDataAsync<CapabilitiesShape>(
            await HttpClient.GetAsync("/api/v1/geo/capabilities"));

        capabilities.SupportedHeatLayers.Should().BeEquivalentTo(
        [
            "community-activity", "housing-availability", "housing-price", "marketplace-activity",
            "jobs", "events", "safety", "cost-of-living"
        ]);
    }

    // ── Plumbing ─────────────────────────────────────────────────────────────────────────────────

    private async Task SignInAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private async Task<HeatShape> GetAsync(string url)
    {
        var response = await HttpClient.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await IdentityApiClient.ReadDataAsync<HeatShape>(response);
    }

    /// <summary>Homes to rent per month at one public point (or none), published at <paramref name="publishedAt"/>.</summary>
    private async Task SeedHomesAsync(
        string country,
        DateTimeOffset publishedAt,
        (double Latitude, double Longitude)? point,
        params decimal[] rents)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var landlordId = LandlordProfileId.New();
        var ownerId = UserId.New();

        foreach (var rent in rents)
        {
            var label = Guid.NewGuid().ToString("N")[..10];

            var address = point is { } at
                ? PropertyAddress.Create(latitude: at.Latitude, longitude: at.Longitude).Value
                : PropertyAddress.Undisclosed;

            var listing = HousingListing.Draft(
                HousingListingId.New(),
                landlordId,
                ownerId,
                HousingTitle.Create($"Heat layer home {label}").Value,
                HousingSlug.Create($"heat-layer-{label}"),
                HousingDescription.Create($"A seeded home for the heat layer suite: {label}.").Value,
                "apartment",
                HousingPrice.Create(rent, CurrencyOf(country), RentPeriod.PerMonth).Value,
                HousingCapacity.Create(1, 1m, maximumOccupants: 2).Value,
                HousingAvailability.Create(null, totalUnits: 1).Value,
                HousingLocationContext.Create(country, null, "Seeded town", null, null, null).Value,
                address,
                StayPolicy.Create(acceptsShortStay: false, acceptsLongStay: true).Value,
                publishedAt).Value;

            listing.AttachMedia(Guid.NewGuid(), HousingMediaRole.Cover, null, publishedAt);
            listing.Publish(publishedAt).IsSuccess.Should().BeTrue("a home that failed to publish is in no layer");

            dbContext.Set<HousingListing>().Add(listing);
        }

        await dbContext.SaveChangesAsync();
    }

    private static string CurrencyOf(string country) => country switch
    {
        "IS" => "ISK",
        "FO" => "DKK",
        "GL" => "DKK",
        _ => "USD"
    };

    private async Task SeedReportAsync(
        (double Latitude, double Longitude) pin, PostCategory category, DateTimeOffset publishedAt, bool corroborated)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var author = UserId.New();

        var post = CommunityPost.Draft(
            author,
            PublishingIdentity.Personal(),
            PostBody.Create($"A seeded safety report for the heat layer suite, {Guid.NewGuid():N}.").Value,
            null,
            category,
            UrgencyLevel.Normal,
            ContentAudience.Public,
            PostLocationContext.Create(
                countryCode: "IS", latitude: pin.Latitude, longitude: pin.Longitude, pointIsExact: true),
            publishedAt).Value;

        post.Publish(publishedAt).IsSuccess.Should().BeTrue();

        if (corroborated)
        {
            var (total, verified) = ConfirmationThresholds.For(UrgencyLevel.Normal);

            for (var index = 0; index < total; index++)
            {
                post.RecordCorroboration(index < verified, 1);
            }

            post.VerificationStatus.Should().Be(PostVerificationStatus.CommunityVerified);
        }

        dbContext.Set<CommunityPost>().Add(post);
        await dbContext.SaveChangesAsync();
    }

    private sealed record HeatShape
    {
        public string Layer { get; init; } = "";

        public string Source { get; init; } = "";

        public string Unit { get; init; } = "";

        public bool IsAvailable { get; init; }

        public string? UnavailableReason { get; init; }

        public List<PointShape> Points { get; init; } = [];

        public int MinimumCellCount { get; init; }

        public int SuppressedCells { get; init; }

        public List<WithheldShape> WithheldCells { get; init; } = [];

        public int? MinimumCountrySample { get; init; }

        public string? Currency { get; init; }

        public string? TimeRange { get; init; }

        public string? TimeBasis { get; init; }

        public DateTimeOffset? From { get; init; }

        public string? Attribution { get; init; }

        public FreshnessShape Freshness { get; init; } = new();
    }

    private sealed record PointShape
    {
        public double Intensity { get; init; }

        public int RawValue { get; init; }

        public string AreaName { get; init; } = "";

        public int? SampleSize { get; init; }

        public Dictionary<string, int>? Categories { get; init; }
    }

    private sealed record WithheldShape
    {
        public string CellKey { get; init; } = "";

        public string Reason { get; init; } = "";

        public string Detail { get; init; } = "";
    }

    private sealed record FreshnessShape
    {
        public string SourceName { get; init; } = "";

        public string? Methodology { get; init; }

        public List<string> Limitations { get; init; } = [];
    }

    private sealed record CapabilitiesShape
    {
        public List<string> SupportedHeatLayers { get; init; } = [];
    }
}
