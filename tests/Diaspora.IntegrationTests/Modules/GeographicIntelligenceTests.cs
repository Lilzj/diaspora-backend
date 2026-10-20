using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
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

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// B21 — geographic intelligence, driven over real HTTP against real rows.
/// <para>
/// <b>Every assertion here seeds both halves of the thing it asserts.</b> Wave 4's most expensive
/// finding was a discovery route that answered 500 whenever its page held one row, passing a suite
/// that only ever asserted emptiness — "a discovery route with no seeded row is a route nobody has
/// tested". A composer is worse in that respect than a filter: with no neighbourhood profile it
/// returns a perfectly well-formed response with every dimension withheld, which is exactly what it
/// should return, and which proves nothing about the composition.
/// </para>
/// <para>
/// So these tests seed assessed profiles, unassessed profiles, and a population to rank against —
/// and assert on what is <em>published</em> as well as on what is withheld and why.
/// </para>
/// <para>
/// The catalogue is B4's own launch geography, seeded by its catalogue runner at host start:
/// Peckham and Woolwich under London, and Manchester's neighbourhoods for the isolation tests.
/// </para>
/// </summary>
public sealed class GeographicIntelligenceTests : IdentityTestBase
{
    private const string Root = "/api/v1/geo";

    /// <summary>A rectangle around Peckham that excludes Woolwich.</summary>
    private const string PeckhamViewport = "north=51.49&south=51.46&east=-0.04&west=-0.10";

    /// <summary>
    /// The country the ranking population is built in.
    /// <para>
    /// Not GB, so the ranks cannot be perturbed by the two British profiles the rest of this class
    /// seeds — the comparison population is scoped by country, and sharing one would make these
    /// assertions depend on execution order.
    /// </para>
    /// </summary>
    private const string RankingCountry = "GH";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GeographicIntelligenceTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Authorization — every route, before anything else
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// No geographic route answers an anonymous caller. A map endpoint must not bypass normal
    /// authorization simply because it is geographic.
    /// </summary>
    [Theory]
    [InlineData("/neighborhoods/peckham/scores")]
    [InlineData("/neighborhoods/peckham/cost-of-living")]
    [InlineData("/neighborhoods/peckham/safety")]
    [InlineData("/neighborhoods/compare?ids=peckham")]
    [InlineData("/capabilities")]
    public async Task An_anonymous_caller_is_refused_every_intelligence_route(string route)
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}{route}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused_the_heat_layer()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync(
            $"{Root}/heatmap?layer=housing-availability&{PeckhamViewport}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_anonymous_caller_is_refused_routing()
    {
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsJsonAsync($"{Root}/routes", new
        {
            origin = new { latitude = 51.47d, longitude = -0.07d },
            destination = new { latitude = 51.49d, longitude = 0.06d },
            travelMode = "walking"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Neighbourhood scores — the composition
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The phase's central claim, against data.</b> B11 holds moderated liveability bands and
    /// the map surfaces never read them. With a profile assessed and approved, the walkability band
    /// arrives as a real <c>transport</c> dimension carrying the band itself, a methodology, the
    /// approver's review date and a sample size — everything §26 requires and the certified route
    /// has never carried.
    /// </summary>
    [Fact]
    public async Task An_assessed_band_is_published_as_a_dimension_with_its_provenance()
    {
        var reviewedAt = await SeedProfileAsync(
            "Peckham",
            NeighborhoodRating.High,
            NeighborhoodRating.Low,
            approve: true);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/peckham/scores");

        var transport = score.Dimensions.Single(dimension => dimension.Id == "transport");

        transport.Score.Should().Be(70, "High is the fourth of five bands");
        transport.Band.Should().Be("High", "the band is the datum and travels beside the number");
        transport.Freshness.SourceName.Should().Be("Diaspora neighbourhood profiles");
        transport.Freshness.Methodology.Should().Contain("moderator");
        transport.Freshness.SampleSize.Should().Be(1, "one assessment, stated plainly");
        transport.Freshness.Limitations.Should().NotBeEmpty();

        transport.Freshness.ComputedAt.Should().BeCloseTo(
            reviewedAt,
            TimeSpan.FromSeconds(5),
            "the profile's review date is a truer computedAt than the instant this request ran");
    }

    /// <summary>
    /// The cost band is a second assessed dimension and encodes independently of the first — a
    /// composer that read one band into both slots would pass a single-band test.
    /// </summary>
    [Fact]
    public async Task Two_assessed_bands_encode_independently()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.VeryHigh, NeighborhoodRating.VeryLow, approve: true);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/peckham/scores");

        score.Dimensions.Single(dimension => dimension.Id == "transport").Score.Should().Be(90);
        score.Dimensions.Single(dimension => dimension.Id == "cost-of-living").Score.Should().Be(10);
    }

    /// <summary>
    /// <b>An unassessed band is withheld, not published as zero.</b> B11's rule is that Unknown is
    /// not a low rating, and a composer that encoded it as one would make every neighbourhood
    /// nobody has looked at read as the worst place on the platform.
    /// </summary>
    [Fact]
    public async Task An_unassessed_band_is_withheld_with_a_reason_rather_than_scored_zero()
    {
        await SeedProfileAsync(
            "Woolwich", NeighborhoodRating.Unknown, NeighborhoodRating.Unknown, approve: true);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/woolwich/scores");

        score.Dimensions.Should().NotContain(dimension => dimension.Id == "transport");

        score.UnavailableDimensions
            .Single(dimension => dimension.Id == "transport")
            .Reason.Should().Be("NotAssessed");
    }

    /// <summary>
    /// <b>Safety is never published, on any neighbourhood, in any state.</b> Nothing on this
    /// platform writes a safety score: B4's column is null on every row, B11's nine bands exclude
    /// safety deliberately, and its published summary hard-codes the field to null. The register
    /// rated safety provenance its highest-consequence item on the premise that the number was
    /// real; this is the assertion that the premise stays inverted.
    /// </summary>
    [Fact]
    public async Task Safety_is_never_a_published_dimension()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.High, NeighborhoodRating.High, approve: true);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/peckham/scores");

        score.Dimensions.Should().NotContain(dimension => dimension.Id == "safety");

        score.UnavailableDimensions
            .Single(dimension => dimension.Id == "safety")
            .Reason.Should().Be("NoDataSource");
    }

    /// <summary>
    /// Every one of the client's nine dimension ids is accounted for exactly once — published or
    /// withheld. A dimension in neither list is one the client renders as a blank with no
    /// explanation, which is the state this whole phase exists to end.
    /// </summary>
    [Fact]
    public async Task Every_dimension_is_either_published_or_explained()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.Moderate, NeighborhoodRating.Moderate, approve: true);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/peckham/scores");

        var accounted = score.Dimensions.Select(dimension => dimension.Id)
            .Concat(score.UnavailableDimensions.Select(dimension => dimension.Id))
            .ToList();

        accounted.Should().BeEquivalentTo(
        [
            "safety", "cost-of-living", "community-activity", "housing",
            "transport", "services", "events", "jobs", "amenities"
        ]);

        accounted.Should().OnlyHaveUniqueItems("a dimension in both lists contradicts itself");
    }

    /// <summary>
    /// <b>The overall figure is marked <c>backend</c> and blended only from what is published.</b>
    /// The client marks its own blend simulated because that one contains invented inputs; this one
    /// does not, and the response says so in the field the client's type already reads.
    /// </summary>
    [Fact]
    public async Task The_overall_figure_is_backend_sourced_and_averages_only_published_dimensions()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.High, NeighborhoodRating.Low, approve: true);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/peckham/scores");

        score.Overall.Should().NotBeNull();
        score.Overall!.Source.Should().Be("backend");

        score.Overall.Value.Should().Be(
            (int)Math.Round(score.Dimensions.Average(dimension => dimension.Score),
                MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// A neighbourhood nobody has described at all publishes no dimension and no overall figure —
    /// <c>null</c> rather than zero, because zero reads as a terrible place.
    /// <para>
    /// The subject is resolved at run time rather than named, because the suite shares one database
    /// across the class and xUnit does not promise an order. Naming a neighbourhood here made this
    /// pass or fail depending on whether the test that assesses that same place had already run.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_neighbourhood_with_no_profile_has_a_null_overall_rather_than_zero()
    {
        var (slug, countryCode) = await UnprofiledNeighborhoodAsync();

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>(
            $"{Root}/neighborhoods/{slug}/scores?countryCode={countryCode}");

        score.Overall.Should().BeNull();
        score.Dimensions.Should().BeEmpty();
        score.UnavailableDimensions.Should().HaveCount(
            9, "every dimension is accounted for even when none can be served");
    }

    /// <summary>
    /// <b>The stable identifier the register asked for.</b> Its complaint was that ids "are
    /// currently derived by slugifying a name, which breaks on rename and collides across cities".
    /// B4's area id is that identifier, it resolves the same place as the slug, and every response
    /// carries it so a client can migrate its links.
    /// </summary>
    [Fact]
    public async Task A_neighbourhood_resolves_by_area_identifier_as_well_as_by_slug()
    {
        await AuthenticateMemberAsync();

        var bySlug = await GetAsync<ScoreDto>($"{Root}/neighborhoods/peckham/scores");
        var byId = await GetAsync<ScoreDto>($"{Root}/neighborhoods/{bySlug.AreaId}/scores");

        byId.AreaId.Should().Be(bySlug.AreaId);
        byId.NeighborhoodId.Should().Be("peckham");
    }

    [Fact]
    public async Task An_unknown_neighbourhood_is_a_not_found_rather_than_an_empty_score()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/neighborhoods/nowhere-at-all/scores");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── The ranked dimensions ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The test the rest of this class needs in order to mean anything.</b> Every other score
    /// test seeds two profiles, which is below the comparison floor, so <c>housing</c> and
    /// <c>community-activity</c> were withheld and the percentile path was never executed — "a
    /// query asserted only against zero rows is unproved". This seeds a real population and asserts
    /// the rank, its explanation and the population size it names.
    /// </summary>
    [Fact]
    public async Task A_ranked_dimension_is_published_once_a_comparison_population_exists()
    {
        var population = await SeedComparisonPopulationAsync(listingCounts: [1, 2, 3, 4, 5, 90]);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>(
            $"{Root}/neighborhoods/{population.BusiestSlug}/scores");

        var housing = score.Dimensions.Single(dimension => dimension.Id == "housing");

        housing.Score.Should().BeGreaterThan(
            50, "the busiest neighbourhood in the set ranks above typical");

        housing.Band.Should().BeNull("a rank is not an assessed band and must not claim to be one");
        housing.Explanation.Should().Contain("90");

        housing.Freshness.SourceName.Should().Be("Diaspora platform content");
        housing.Freshness.SampleSize.Should().BeGreaterThanOrEqualTo(6);
        housing.Freshness.Methodology.Should().Contain("Percentile rank");
    }

    /// <summary>
    /// <b>The methodology names the population it actually used, including the awkward part.</b>
    /// B11's directory returns the most active profiled neighbourhoods that have at least one live
    /// listing — a narrower set than "every profiled neighbourhood" — and a provenance sentence
    /// that overstated its population would be worse than none, because a reader would believe it.
    /// </summary>
    [Fact]
    public async Task A_ranked_dimensions_methodology_states_the_population_it_actually_used()
    {
        var population = await SeedComparisonPopulationAsync(listingCounts: [1, 2, 3, 4, 5, 90]);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>(
            $"{Root}/neighborhoods/{population.BusiestSlug}/scores");

        var methodology = score.Dimensions
            .Single(dimension => dimension.Id == "housing")
            .Freshness.Methodology;

        methodology.Should().Contain("at least one live housing listing");
        methodology.Should().Contain("most active");
        methodology.Should().Contain("50 means typical");
    }

    /// <summary>
    /// Below the configured floor a rank is noise wearing a number's clothes, so it is withheld
    /// with a reason rather than published.
    /// </summary>
    [Fact]
    public async Task A_ranked_dimension_is_withheld_when_the_population_is_too_small()
    {
        var slug = await SeedIsolatedProfileWithListingsAsync();

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/{slug}/scores?countryCode=NG");

        score.Dimensions.Should().NotContain(dimension => dimension.Id == "housing");

        score.UnavailableDimensions
            .Single(dimension => dimension.Id == "housing")
            .Reason.Should().Be("InsufficientPopulation");
    }

    /// <summary>
    /// <b>A vertical that reported a real count keeps the count and loses only the score.</b> Jobs,
    /// events and services have no cheap comparison population, so they are withheld from the
    /// breakdown with <c>NoComparableScale</c> — and the figure itself travels on
    /// <c>resourceCounts</c>, which is what B4 already serves.
    /// </summary>
    [Fact]
    public async Task A_count_with_no_scale_is_withheld_as_a_score_and_kept_as_a_count()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.High, NeighborhoodRating.Low, approve: true);

        await AuthenticateMemberAsync();

        var score = await GetAsync<ScoreDto>($"{Root}/neighborhoods/peckham/scores");

        foreach (var (dimension, resourceType) in new[]
                 {
                     ("jobs", "jobs"), ("events", "events"), ("services", "providers")
                 })
        {
            score.Dimensions.Should().NotContain(entry => entry.Id == dimension);

            score.UnavailableDimensions
                .Single(entry => entry.Id == dimension)
                .Reason.Should().Be("NoComparableScale");

            score.ResourceCounts.Should().ContainKey(
                resourceType,
                "the count is real and published even where no score can be — including jobs, "
                + "whose counter B21 registered");
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Comparison
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Each subject arrives composed identically, which is what makes a fair comparison possible.
    /// The server declares no winner and returns no ranking.
    /// </summary>
    [Fact]
    public async Task A_comparison_returns_each_subject_composed_the_same_way()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.High, NeighborhoodRating.Low, approve: true);
        await SeedProfileAsync(
            "Woolwich", NeighborhoodRating.Low, NeighborhoodRating.High, approve: true);

        await AuthenticateMemberAsync();

        var page = await GetAsync<List<ScoreDto>>(
            $"{Root}/neighborhoods/compare?ids=peckham,woolwich");

        page.Should().HaveCount(2);
        page.Select(entry => entry.NeighborhoodId).Should().BeEquivalentTo(["peckham", "woolwich"]);

        page.Should().AllSatisfy(entry =>
            entry.UnavailableDimensions.Should().Contain(
                dimension => dimension.Id == "safety",
                "every subject states the same gaps, or the comparison is not like for like"));
    }

    /// <summary>An unresolvable subject is dropped; the rest of the comparison still answers.</summary>
    [Fact]
    public async Task A_comparison_drops_an_unknown_subject_rather_than_failing()
    {
        await AuthenticateMemberAsync();

        var page = await GetAsync<List<ScoreDto>>(
            $"{Root}/neighborhoods/compare?ids=peckham,nowhere-at-all");

        page.Should().HaveCount(1);
        page[0].NeighborhoodId.Should().Be("peckham");
    }

    [Fact]
    public async Task A_comparison_of_nothing_is_refused()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.GetAsync($"{Root}/neighborhoods/compare?ids=");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A comparison is bounded, so it cannot become a fan of profile reads.</summary>
    [Fact]
    public async Task A_comparison_beyond_the_configured_ceiling_is_refused()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/neighborhoods/compare?ids=a,b,c,d,e,f,g,h,i,j,k,l");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Cost of living
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The real band is served and no index is derived from it.</b> B11 holds one moderated
    /// ordinal band; the client's surface wants category indices against a national baseline in a
    /// stated currency, and publishing an index derived from a band would be a claim about how much
    /// more expensive somewhere is, to the percent, against a baseline nobody measured. Since 17 Sep
    /// 2026 a rent index is published from listings where there are enough of them
    /// (<c>CostOfLivingRentIndexTests</c>); this suite seeds none, so there is none here.
    /// </summary>
    [Fact]
    public async Task The_cost_band_is_real_and_the_index_is_explicitly_absent()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.Moderate, NeighborhoodRating.High, approve: true);

        await AuthenticateMemberAsync();

        var cost = await GetAsync<CostDto>($"{Root}/neighborhoods/peckham/cost-of-living");

        cost.Band.Should().Be("High");
        cost.BandScore.Should().Be(70);

        cost.IsAvailable.Should().BeFalse();
        cost.OverallIndex.Should().BeNull();
        cost.BaselineName.Should().BeNull();
        cost.Currency.Should().BeNull();
        cost.Categories.Should().BeEmpty("five of six categories have no source, so none is served");

        cost.Freshness.Limitations.Should().NotBeEmpty();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The safety surface answers, and its answer is that there is no score — with a branchable
    /// reason and the platform's own limitations text, so the client can stop writing its own.
    /// </summary>
    [Fact]
    public async Task The_safety_surface_answers_unavailable_with_server_owned_limitations()
    {
        await SeedProfileAsync(
            "Peckham", NeighborhoodRating.VeryHigh, NeighborhoodRating.VeryHigh, approve: true);

        await AuthenticateMemberAsync();

        var safety = await GetAsync<SafetyDto>($"{Root}/neighborhoods/peckham/safety");

        safety.IsAvailable.Should().BeFalse();
        safety.Score.Should().BeNull("a fully assessed profile still produces no safety score");
        safety.Band.Should().BeNull();
        safety.Breakdown.Should().BeEmpty();
        safety.UnavailableReason.Should().Be("NoDataSource");

        safety.Limitations.Should().HaveCountGreaterThan(
            2, "§26 requires what the figure does not measure to be published, not implied");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Heat layers
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The layer answers in the shape the client declared, over B4's real cells, with the
    /// suppression floor and scale statement published rather than assumed.
    /// </summary>
    [Fact]
    public async Task A_heat_layer_returns_normalised_cells_with_its_scale_and_suppression_stated()
    {
        await AuthenticateMemberAsync();

        var layer = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=housing-availability&{PeckhamViewport}");

        layer.Layer.Should().Be("housing-availability");
        layer.Unit.Should().Be("listings");
        layer.Scale.Should().Be("relative-to-viewport");
        layer.MinimumCellCount.Should().BeGreaterThan(0);
        layer.Bounds.North.Should().Be(51.49d);
        layer.Bounds.West.Should().Be(-0.10d);
        layer.Freshness.SourceName.Should().NotBeNullOrWhiteSpace();
        layer.Freshness.Limitations.Should().NotBeEmpty();

        layer.Points.Should().AllSatisfy(point =>
        {
            point.Intensity.Should().BeInRange(0d, 1d);
            point.AreaId.Should().NotBe(Guid.Empty, "a cell is a place, not a grid tile");
        });
    }

    /// <summary>
    /// <b>Jobs is a real layer because B21 registered the counter Jobs never contributed.</b> It
    /// was the one vertical of seven with no <c>ILocationResourceCounter</c>, so a jobs layer had
    /// no source and a neighbourhood hub rendered a dash — indistinguishable from a vertical that
    /// was merely slow.
    /// </summary>
    [Fact]
    public async Task The_jobs_layer_is_supported_because_jobs_now_registers_a_counter()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/heatmap?layer=jobs&{PeckhamViewport}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var capabilities = await GetAsync<CapabilitiesDto>($"{Root}/capabilities");

        capabilities.SupportedHeatLayers.Should().Contain("jobs");
    }

    /// <summary>
    /// <b>The client's <c>zoom</c> is honoured, not ignored.</b> <c>fetchHeatLayer</c> has sent it
    /// since Phase 8. Below the threshold a neighbourhood cell is a pin on top of another pin, so
    /// the rung becomes the city — which is the client's own statement about its viewport, not the
    /// server guessing at a box it cannot see the pixels of.
    /// </summary>
    [Theory]
    [InlineData(4, "City")]
    [InlineData(16, "Neighborhood")]
    public async Task The_clients_zoom_selects_the_rung(int zoom, string expected)
    {
        await AuthenticateMemberAsync();

        var layer = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=housing-availability&{PeckhamViewport}&zoom={zoom}");

        layer.Level.Should().Be(expected);
    }

    /// <summary>An explicit level wins over a zoom, because the client meant it.</summary>
    [Fact]
    public async Task An_explicit_level_overrides_the_zoom()
    {
        await AuthenticateMemberAsync();

        var layer = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=housing-availability&{PeckhamViewport}&zoom=4&level=Neighborhood");

        layer.Level.Should().Be("Neighborhood");
    }

    /// <summary>
    /// <b>A layer with no source is refused, not answered empty.</b> An empty heat layer renders as
    /// a quiet neighbourhood, which is a claim about the world; a named error is a claim about the
    /// platform, and it is the true one.
    /// <para>
    /// <c>safety</c>, <c>cost-of-living</c> and <c>housing-price</c> stood here until 21 Oct 2026, when
    /// each got a real source (<c>HeatLayerSourcesTests</c>). A layer nobody computes still gets this.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("crime-score")]
    [InlineData("unicorns")]
    public async Task A_layer_with_no_real_source_is_refused_rather_than_answered_empty(string layer)
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.GetAsync($"{Root}/heatmap?layer={layer}&{PeckhamViewport}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("geo.layer.unsupported");
    }

    /// <summary>
    /// A public endpoint that accepted the whole planet would be a full catalogue scan with a
    /// friendly name.
    /// </summary>
    [Fact]
    public async Task A_viewport_wider_than_the_configured_maximum_is_refused()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/heatmap?layer=housing-availability&north=80&south=-80&east=170&west=-170");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("geo.bounds.too_large");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Routing — the provider boundary
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The point of the whole routing seam.</b> No vendor is contracted, so the route answers
    /// <c>NotConfigured</c> — and carries no route, no distance and no duration. Every input needed
    /// to fabricate a plausible travel time is present in the request; none of it is used.
    /// </summary>
    [Fact]
    public async Task Routing_reports_not_configured_and_returns_no_fabricated_travel_time()
    {
        await AuthenticateMemberAsync();

        var route = await PostAsync<RouteDto>($"{Root}/routes", new
        {
            origin = new { latitude = 51.4742d, longitude = -0.0691d },
            destination = new { latitude = 51.4933d, longitude = 0.0632d },
            travelMode = "walking"
        });

        route.Status.Should().Be("NotConfigured");
        route.Provider.Should().Be("none");
        route.IsRouted.Should().BeFalse();
        route.Routes.Should().BeEmpty();
        route.Message.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>Every travel mode answers the same way, including the one with the least coverage.</summary>
    [Theory]
    [InlineData("walking")]
    [InlineData("cycling")]
    [InlineData("driving")]
    [InlineData("transit")]
    public async Task Every_travel_mode_reports_not_configured_rather_than_estimating(string mode)
    {
        await AuthenticateMemberAsync();

        var route = await PostAsync<RouteDto>($"{Root}/routes", new
        {
            origin = new { latitude = 51.4742d, longitude = -0.0691d },
            destination = new { latitude = 51.4933d, longitude = 0.0632d },
            travelMode = mode
        });

        route.Status.Should().Be("NotConfigured");
        route.TravelMode.Should().BeEquivalentTo(mode);
        route.Routes.Should().BeEmpty();
    }

    /// <summary>
    /// <b>A longitude in a latitude field is the malformed coordinate that is silent everywhere
    /// else</b>, because both are plausible numbers. Latitude stops at ±90 and the check runs
    /// before any provider is asked.
    /// </summary>
    [Fact]
    public async Task A_longitude_value_in_the_latitude_field_is_refused()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Root}/routes", new
        {
            origin = new { latitude = 145.7d, longitude = -0.07d },
            destination = new { latitude = 51.49d, longitude = 0.06d },
            travelMode = "walking"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("geo.coordinate.out_of_range");
    }

    [Theory]
    [InlineData(51.47, 181.0)]
    [InlineData(-91.0, -0.07)]
    public async Task An_out_of_range_coordinate_is_refused(double latitude, double longitude)
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Root}/routes", new
        {
            origin = new { latitude, longitude },
            destination = new { latitude = 51.49d, longitude = 0.06d },
            travelMode = "walking"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>A missing origin is a bad request, never a route from nowhere.</summary>
    [Fact]
    public async Task A_missing_origin_is_refused()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Root}/routes", new
        {
            destination = new { latitude = 51.49d, longitude = 0.06d },
            travelMode = "walking"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_unknown_travel_mode_is_refused()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Root}/routes", new
        {
            origin = new { latitude = 51.4742d, longitude = -0.0691d },
            destination = new { latitude = 51.4933d, longitude = 0.0632d },
            travelMode = "teleport"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("geo.travel_mode.unsupported");
    }

    /// <summary>
    /// Bounded before the provider is called, so an accidental request for a route across an ocean
    /// costs one trigonometric function rather than a vendor call and a timeout.
    /// </summary>
    [Fact]
    public async Task A_route_longer_than_the_configured_maximum_is_refused()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.PostAsJsonAsync($"{Root}/routes", new
        {
            origin = new { latitude = 51.47d, longitude = -0.07d },
            destination = new { latitude = -33.87d, longitude = 151.21d },
            travelMode = "driving"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("geo.route.too_long");
    }

    [Fact]
    public async Task More_waypoints_than_the_configured_ceiling_are_refused()
    {
        await AuthenticateMemberAsync();

        var waypoints = Enumerable.Range(0, 20)
            .Select(step => new { latitude = 51.47d + (step * 0.001d), longitude = -0.07d })
            .ToArray();

        var response = await HttpClient.PostAsJsonAsync($"{Root}/routes", new
        {
            origin = new { latitude = 51.47d, longitude = -0.07d },
            destination = new { latitude = 51.49d, longitude = -0.06d },
            travelMode = "walking",
            waypoints
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("geo.route.too_many_waypoints");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Capabilities
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The report tells the truth about what is missing, which is most of it.</b> A client that
    /// reads this before rendering a Directions button never renders one that fails — and the
    /// distinction between "no service here" and "we do not know" cannot be made from a failed
    /// request, only from a declaration.
    /// </summary>
    [Fact]
    public async Task The_capability_report_declares_what_is_configured_and_what_is_not()
    {
        await AuthenticateMemberAsync();

        var capabilities = await GetAsync<CapabilitiesDto>($"{Root}/capabilities");

        Available(capabilities, "routing").Should().BeFalse();
        Available(capabilities, "transit-routing").Should().BeFalse();
        Available(capabilities, "safety-scores").Should().BeFalse();
        Available(capabilities, "street-level-geocoding").Should().BeFalse();

        Available(capabilities, "neighborhood-scores").Should().BeTrue();
        Available(capabilities, "heat-layers").Should().BeTrue();

        // Since 17 Sep 2026: rent from Diaspora's own listings, and nothing else — the detail says
        // so, because "available" alone would read as the whole cost of living.
        Available(capabilities, "cost-of-living").Should().BeTrue();
        capabilities.Capabilities.Single(entry => entry.Capability == "cost-of-living").Detail
            .Should().StartWith("Rent only");

        capabilities.SupportedTravelModes.Should().BeEmpty(
            "a provider that answers no mode must not list four");
    }

    /// <summary>
    /// <b>No provider configuration leaves the module.</b> The report publishes a stable key the
    /// deployment chose and nothing else — no endpoint, no credential, no quota.
    /// </summary>
    [Fact]
    public async Task The_capability_report_exposes_no_provider_configuration()
    {
        await AuthenticateMemberAsync();

        var body = await (await HttpClient.GetAsync($"{Root}/capabilities")).Content
            .ReadAsStringAsync();

        body.Should().NotContainAny("http://", "https://", "apiKey", "api_key", "secret", "token");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Country isolation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A country filter on the viewport keeps another country's places out.</b> The catalogue
    /// spans countries and a density layer that leaked across one would put a member's map in the
    /// wrong hemisphere.
    /// </summary>
    [Fact]
    public async Task A_country_scoped_heat_layer_excludes_another_countrys_places()
    {
        await AuthenticateMemberAsync();

        var british = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=housing-availability&{PeckhamViewport}&countryCode=GB");

        var nigerian = await GetAsync<HeatDto>(
            $"{Root}/heatmap?layer=housing-availability&{PeckhamViewport}&countryCode=NG");

        nigerian.Points.Should().BeEmpty(
            "no Nigerian place sits inside a rectangle drawn around Peckham");

        british.Points.Select(point => point.AreaId)
            .Should().NotIntersectWith(nigerian.Points.Select(point => point.AreaId));
    }

    /// <summary>
    /// A slug is unique within a country, not globally. Scoping the lookup is what stops the wrong
    /// Springfield answering.
    /// </summary>
    [Fact]
    public async Task A_slug_scoped_to_the_wrong_country_does_not_resolve()
    {
        await AuthenticateMemberAsync();

        var response = await HttpClient.GetAsync(
            $"{Root}/neighborhoods/peckham/scores?countryCode=NG");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Seeding & plumbing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Assesses a neighbourhood profile against B4's real area, creating one if none exists.
    /// <para>
    /// Joined on <c>AreaId</c> — B4's identifier, which is the whole point of B11's seam — so the
    /// profile is discoverable by exactly the route the composer takes.
    /// </para>
    /// <para>
    /// <b>It updates in place rather than replacing, and that is not a stylistic choice.</b> The
    /// first version deleted the existing profile and inserted a fresh one, which fails on the
    /// second test in the class with <c>UNIQUE constraint failed: housing_neighborhoods.AreaId</c>.
    /// <c>NeighborhoodProfile</c> is soft-deletable, so <c>Remove</c> is intercepted into a flag
    /// update: the row stays, still holding the area, and the unique index refuses the insert. The
    /// suite shares one database across the class, so the defect is invisible when a test is run on
    /// its own — which is exactly how it presented.
    /// </para>
    /// </summary>
    private async Task<DateTimeOffset> SeedProfileAsync(
        string neighborhood,
        NeighborhoodRating walkability,
        NeighborhoodRating costOfLiving,
        bool approve)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var area = await dbContext.Set<AdministrativeArea>()
            .AsNoTracking()
            .FirstAsync(node => node.Level == AdministrativeLevel.Neighborhood
                && node.Name.Value == neighborhood);

        var now = DateTimeOffset.UtcNow;

        var profile = await dbContext.Set<NeighborhoodProfile>()
            .FirstOrDefaultAsync(candidate => candidate.AreaId == area.Id.Value);

        if (profile is null)
        {
            profile = NeighborhoodProfile.Create(
                NeighborhoodProfileId.New(),
                area.Id.Value,
                area.CountryCode.Value,
                area.Name.Value,
                area.Slug.Value,
                now).Value;

            dbContext.Set<NeighborhoodProfile>().Add(profile);
        }

        profile.UpdateLivingIndex(
            NeighborhoodLivingIndex.Create(
                walkability: walkability,
                costOfLiving: costOfLiving),
            UserId.New(),
            now).IsSuccess.Should().BeTrue();

        if (approve)
        {
            // Approving is what stamps LastReviewedAt, which the composer publishes as computedAt.
            profile.Approve(UserId.New(), now).IsSuccess.Should().BeTrue();
        }

        await dbContext.SaveChangesAsync();

        return now;
    }

    /// <summary>
    /// Builds a comparison population large enough to rank against, in its own country.
    /// <para>
    /// <b>Its own country, so it cannot be perturbed by the profiles the rest of the class
    /// seeds.</b> The population is scoped by country, so putting these in GB alongside Peckham and
    /// Woolwich would make the ranks depend on execution order. Areas are created directly under
    /// the country rather than borrowed from the catalogue, because B4's launch geography does not
    /// contain six neighbourhoods in one country to spare.
    /// </para>
    /// <para>
    /// Every profile carries a listing count, because B11's directory returns only neighbourhoods
    /// with at least one live listing — a profile with none would be absent from the very
    /// population this method exists to build.
    /// </para>
    /// </summary>
    private async Task<(string BusiestSlug, int Size)> SeedComparisonPopulationAsync(
        int[] listingCounts)
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;

        // Through the repository, not a LINQ predicate. `candidate.Code.Value == "GH"` reaches
        // through a value converter and does not translate — the same trap, third time in this file.
        var countries = scope.ServiceProvider.GetRequiredService<ICountryRepository>();

        var country = await countries.GetByCodeAsync(CountryCode.TryCreate(RankingCountry).Value);

        country.Should().NotBeNull($"B4 seeds {RankingCountry} in its launch catalogue");

        var existing = await dbContext.Set<AdministrativeArea>()
            .AsNoTracking()
            .Where(node => node.Level == AdministrativeLevel.Neighborhood)
            .ToListAsync();

        var busiest = string.Empty;
        var busiestCount = -1;

        for (var index = 0; index < listingCounts.Length; index++)
        {
            var slug = $"rank-{index}";

            var area = existing.Find(node =>
                node.Slug.Value == slug && node.CountryCode.Value == RankingCountry);

            if (area is null)
            {
                area = AdministrativeArea.CreateUnderCountry(
                    country,
                    AdministrativeLevel.Neighborhood,
                    PlaceName.Create($"Rank {index}"),
                    LocationSlug.TryCreate(slug).Value).Value;

                dbContext.Set<AdministrativeArea>().Add(area);
                await dbContext.SaveChangesAsync();
            }

            var profile = await dbContext.Set<NeighborhoodProfile>()
                .FirstOrDefaultAsync(candidate => candidate.AreaId == area.Id.Value);

            if (profile is null)
            {
                profile = NeighborhoodProfile.Create(
                    NeighborhoodProfileId.New(),
                    area.Id.Value,
                    RankingCountry,
                    area.Name.Value,
                    area.Slug.Value,
                    now).Value;

                dbContext.Set<NeighborhoodProfile>().Add(profile);
            }

            profile.SetListingCount(listingCounts[index]);

            if (listingCounts[index] > busiestCount)
            {
                busiestCount = listingCounts[index];
                busiest = slug;
            }
        }

        await dbContext.SaveChangesAsync();

        return (busiest, listingCounts.Length);
    }

    /// <summary>
    /// One profiled neighbourhood in a country with no others, so the comparison population is
    /// below the floor and the rank must be withheld.
    /// </summary>
    private async Task<string> SeedIsolatedProfileWithListingsAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var now = DateTimeOffset.UtcNow;
        const string slug = "lonely-place";

        var countries = scope.ServiceProvider.GetRequiredService<ICountryRepository>();

        var country = await countries.GetByCodeAsync(CountryCode.TryCreate("NG").Value);

        country.Should().NotBeNull("B4 seeds NG in its launch catalogue");

        var areas = await dbContext.Set<AdministrativeArea>()
            .AsNoTracking()
            .Where(node => node.Level == AdministrativeLevel.Neighborhood)
            .ToListAsync();

        var area = areas.Find(node => node.Slug.Value == slug);

        if (area is null)
        {
            area = AdministrativeArea.CreateUnderCountry(
                country,
                AdministrativeLevel.Neighborhood,
                PlaceName.Create("Lonely Place"),
                LocationSlug.TryCreate(slug).Value).Value;

            dbContext.Set<AdministrativeArea>().Add(area);
            await dbContext.SaveChangesAsync();
        }

        var profile = await dbContext.Set<NeighborhoodProfile>()
            .FirstOrDefaultAsync(candidate => candidate.AreaId == area.Id.Value);

        if (profile is null)
        {
            profile = NeighborhoodProfile.Create(
                NeighborhoodProfileId.New(),
                area.Id.Value,
                "NG",
                area.Name.Value,
                area.Slug.Value,
                now).Value;

            dbContext.Set<NeighborhoodProfile>().Add(profile);
        }

        profile.SetListingCount(4);

        await dbContext.SaveChangesAsync();

        return slug;
    }

    /// <summary>
    /// A neighbourhood nobody has written a profile for, with the country that disambiguates it.
    /// <para>
    /// Found rather than named, so the test that uses it does not depend on which of its siblings
    /// has already run against the shared database.
    /// </para>
    /// <para>
    /// <b>Everything but the level is filtered in memory, and that is not laziness.</b> Neither
    /// <c>profiled.Contains(node.Id)</c> nor <c>node.CountryCode.Value == "GB"</c> translates:
    /// both reach through a value converter, which EF cannot see inside, and both compile and read
    /// perfectly. B4 seeds nineteen launch nodes, so materialising them costs nothing.
    /// </para>
    /// </summary>
    private async Task<(string Slug, string CountryCode)> UnprofiledNeighborhoodAsync()
    {
        Factory.EnsureDatabaseCreated();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var profiled = await dbContext.Set<NeighborhoodProfile>()
            .AsNoTracking()
            .Select(profile => profile.AreaId)
            .ToListAsync();

        var candidates = await dbContext.Set<AdministrativeArea>()
            .AsNoTracking()
            .Where(node => node.Level == AdministrativeLevel.Neighborhood)
            .ToListAsync();

        var area = candidates.Find(node => !profiled.Contains(node.Id.Value));

        area.Should().NotBeNull(
            "B4 seeds several neighbourhoods and this suite profiles only two of them");

        return (area!.Slug.Value, area.CountryCode.Value);
    }

    private async Task AuthenticateMemberAsync()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);
    }

    private static bool Available(CapabilitiesDto capabilities, string capability) =>
        capabilities.Capabilities.Single(entry => entry.Capability == capability).Available;

    private async Task<T> GetAsync<T>(string route) =>
        await ReadDataAsync<T>(await HttpClient.GetAsync(route));

    private async Task<T> PostAsync<T>(string route, object body) =>
        await ReadDataAsync<T>(await HttpClient.PostAsJsonAsync(route, body));

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            $"the request should have succeeded but returned {(int)response.StatusCode}: {body}");

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    // ── Wire shapes, named as the client names them ─────────────────────────────────────────

    private sealed record ScoreDto(
        string NeighborhoodId,
        Guid AreaId,
        string Name,
        SourcedDto? Overall,
        List<DimensionDto> Dimensions,
        List<UnavailableDto> UnavailableDimensions,
        Dictionary<string, int> ResourceCounts,
        FreshnessDto Freshness);

    private sealed record SourcedDto(int Value, string Source);

    private sealed record DimensionDto(
        string Id, string Label, int Score, string? Band, string? Explanation, FreshnessDto Freshness);

    private sealed record UnavailableDto(string Id, string Label, string Reason, string Explanation);

    private sealed record FreshnessDto(
        DateTimeOffset ComputedAt,
        string SourceName,
        string? Methodology,
        List<string> Limitations,
        DateTimeOffset? ValidUntil,
        int? SampleSize);

    private sealed record CostDto(
        string NeighborhoodId,
        bool IsAvailable,
        string? Band,
        int? BandScore,
        int? OverallIndex,
        string? BaselineName,
        string? Currency,
        List<object> Categories,
        FreshnessDto Freshness);

    private sealed record SafetyDto(
        string NeighborhoodId,
        bool IsAvailable,
        int? Score,
        string? Band,
        List<object> Breakdown,
        string? UnavailableReason,
        List<string> Limitations);

    private sealed record HeatDto(
        string Layer,
        string Unit,
        List<HeatPointDto> Points,
        string Scale,
        int MinValue,
        int MaxValue,
        string Level,
        int MinimumCellCount,
        int SuppressedCells,
        bool Truncated,
        BoundsDto Bounds,
        FreshnessDto Freshness);

    private sealed record HeatPointDto(
        double Latitude, double Longitude, double Intensity, int RawValue, string AreaName, Guid AreaId);

    private sealed record BoundsDto(double North, double South, double East, double West);

    private sealed record CapabilitiesDto(
        List<CapabilityDto> Capabilities,
        List<string> SupportedTravelModes,
        List<string> SupportedHeatLayers,
        List<string> SupportedScoreDimensions);

    private sealed record CapabilityDto(
        string Capability, bool Available, string Provider, string Detail);

    private sealed record RouteDto(
        string Status,
        string TravelMode,
        string Provider,
        List<object> Routes,
        bool IsRouted,
        string? Message);
}
