using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Housing, Accommodation &amp; Neighborhood Platform (B11), driven over real HTTP through the
/// same host every other module suite uses.
/// <para>
/// Three claims are under test. That the module composed into the platform through nothing but
/// contributor interfaces — its gates appear in Identity's catalogue and its name in the manifest,
/// with no edit to the kernel, to Identity, or to <c>Program.cs</c>. That the two catalogues the
/// brief calls configurable really are seeded reference data a client can render a search form
/// from. And that the access band holds: <b>looking is Limited, contributing is Limited, listing
/// and viewing and reserving are Verified, moderating is staff.</b>
/// </para>
/// <para>
/// <b>B11 is the first vertical since B7 that collides with nothing in the provisional
/// catalogue</b>, so unlike B8, B9 and B10 it deletes no host-declared gate on its way in. The
/// duplicate-gate assertion is kept anyway: it is cheap, and the failure it catches takes down
/// phone verification rather than anything that looks like housing.
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding
/// these tests to B11's own response records would make them pass whenever the module compiles,
/// which is the one thing a contract test must never do.
/// </para>
/// </summary>
public sealed class HousingModuleTests : IdentityTestBase
{
    private const string Root = "/api/v1/housing";

    public HousingModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: gates B11 owns
    /// turn up in Identity's response, which the Housing module never touched.
    /// </summary>
    [Fact]
    public async Task The_housing_feature_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("housing.browse", "browsing is the entry point to the module");
        names.Should().Contain("housing.listings.create");
        names.Should().Contain("housing.viewing");
        names.Should().Contain("housing.reserve");
        names.Should().Contain("housing.neighborhood.contribute");
        names.Should().Contain("housing.moderate");
    }

    /// <summary>
    /// No gate is declared twice. Two modules declaring one name fails the catalogue at startup and
    /// takes down every request that resolves it — phone verification included, which is how this
    /// very test creates its member. Reaching the assertion at all is most of the proof.
    /// </summary>
    [Fact]
    public async Task No_housing_gate_is_declared_twice()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        foreach (var name in features.Features
                     .Select(feature => feature.Name)
                     .Where(name => name.StartsWith("housing.", StringComparison.Ordinal)))
        {
            features.Features.Where(feature => feature.Name == name)
                .Should().ContainSingle($"'{name}' must be declared by exactly one contributor");
        }
    }

    /// <summary>
    /// <b>The access band, read straight off the catalogue for a member who has verified a phone
    /// and nothing else.</b> Looking and contributing are open to them; the three that cost
    /// somebody money or a home are not.
    /// </summary>
    [Fact]
    public async Task A_limited_member_may_look_and_contribute_but_not_list_view_or_reserve()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        Enabled(features, "housing.browse").Should().BeTrue(
            "somebody deciding whether they can afford to move must be able to look first");

        Enabled(features, "housing.neighborhood.contribute").Should().BeTrue(
            "what makes neighbourhood insights worth having is that ordinary residents write them");

        Enabled(features, "housing.listings.create").Should().BeFalse(
            "a listing collects deposits from strangers who have not arrived in the country yet");

        Enabled(features, "housing.viewing").Should().BeFalse(
            "this is the gate that guards addresses");

        Enabled(features, "housing.reserve").Should().BeFalse(
            "a reservation takes a home off the market");
    }

    [Fact]
    public async Task The_listing_viewing_and_reserving_gates_open_for_a_verified_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        Enabled(features, "housing.listings.create").Should().BeTrue();
        Enabled(features, "housing.viewing").Should().BeTrue();
        Enabled(features, "housing.reserve").Should().BeTrue();
    }

    /// <summary>
    /// <b>Moderation stays shut for a Verified member, and that is B2.0.1's distinction made
    /// visible.</b> A tier measures how far somebody has progressed; a role grants a privilege. A
    /// gate that merely raised the tier would tell an ordinary member that verifying unlocks the
    /// moderation queue — and then every member who verified would ask why it had not.
    /// </summary>
    [Fact]
    public async Task Moderation_stays_shut_however_far_a_member_progresses()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        Enabled(features, "housing.moderate").Should().BeFalse(
            "staff-only is not a tier a member can climb to");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_housing_module()
    {
        var response = await HttpClient.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(
            "Housing", "the kernel discovered the module and published it in the manifest");
    }

    // ── The configurable catalogues ─────────────────────────────────────────────────────────

    /// <summary>
    /// The accommodation-type catalogue arrived through <c>IAccommodationTypeContributor</c>.
    /// Without it no property could be drafted, because a type key is required.
    /// </summary>
    [Fact]
    public async Task The_accommodation_type_catalogue_seeded_at_startup()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/accommodation-types");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var types = await IdentityApiClient.ReadDataAsync<List<AccommodationTypeShape>>(response);

        types.Select(type => type.Key).Should().Contain(
            [
                "apartment", "studio", "house", "shared-apartment", "private-room",
                "roommate-wanted", "family-stay", "hostel", "student-hostel",
                "temporary-accommodation"
            ],
            "the shipped catalogue leads with how people actually arrive in a country — a room in "
            + "somebody's flat and a hostel bed long before a whole house");
    }

    /// <summary>
    /// <b>The two flags a search form is built from.</b> Shared decides whether the roommate
    /// section renders at all, and temporary decides whether the form asks for a lease or a number
    /// of nights.
    /// </summary>
    [Fact]
    public async Task An_accommodation_type_declares_whether_it_is_shared_and_whether_it_is_temporary()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var types = await IdentityApiClient.ReadDataAsync<List<AccommodationTypeShape>>(
            await HttpClient.GetAsync($"{Root}/accommodation-types"));

        types.Single(type => type.Key == "private-room").IsShared.Should().BeTrue();
        types.Single(type => type.Key == "apartment").IsShared.Should().BeFalse();

        types.Single(type => type.Key == "hostel").IsTemporary.Should().BeTrue();
        types.Single(type => type.Key == "house").IsTemporary.Should().BeFalse();
    }

    /// <summary>
    /// The amenity catalogue arrived through <c>IHousingAmenityContributor</c>, grouped as a
    /// listing form renders it.
    /// </summary>
    [Fact]
    public async Task The_amenity_catalogue_seeded_grouped_and_ordered()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/amenities");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var amenities = await IdentityApiClient.ReadDataAsync<List<AmenityShape>>(response);

        amenities.Select(amenity => amenity.Key).Should().Contain(
            ["wifi", "kitchen", "laundry", "water-heater", "generator", "security", "parking"],
            "the shipped list is what a member in Lagos or Berlin actually filters on");

        amenities.Should().OnlyContain(
            amenity => amenity.Group != null && amenity.Group.Length > 0,
            "an ungrouped amenity has nowhere to render on the form");
    }

    /// <summary>
    /// <b>Accessibility features are flagged rather than left as ordinary amenities.</b> A member
    /// who needs step-free access is not browsing for a nice-to-have, and the flag is what lets a
    /// client render them as a filter of their own instead of item forty in a checkbox list.
    /// </summary>
    [Fact]
    public async Task The_accessibility_amenities_are_flagged_as_such()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var amenities = await IdentityApiClient.ReadDataAsync<List<AmenityShape>>(
            await HttpClient.GetAsync($"{Root}/amenities"));

        var accessible = amenities
            .Where(amenity => amenity.IsAccessibilityFeature)
            .Select(amenity => amenity.Key)
            .ToList();

        accessible.Should().Contain(
            ["wheelchair-access", "step-free-entrance", "accessible-bathroom"]);

        amenities.Single(amenity => amenity.Key == "wifi")
            .IsAccessibilityFeature.Should().BeFalse();
    }

    /// <summary>
    /// The catalogues answer an anonymous caller, because a client needs the pickers before it can
    /// render a search form at all — and both are reference data rather than anybody's content.
    /// </summary>
    [Fact]
    public async Task The_catalogues_answer_without_a_token()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"{Root}/accommodation-types"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await HttpClient.GetAsync($"{Root}/amenities"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>The write half of "configurable".</b> A catalogue that can only be changed by shipping a
    /// new contributor is configurable to whoever can deploy and to nobody else, and the brief asked
    /// for configurable. The route exists and is shut to a member who merely verified.
    /// </summary>
    [Theory]
    [InlineData("/accommodation-types")]
    [InlineData("/amenities")]
    public async Task The_catalogue_write_routes_exist_and_are_shut_to_a_member(string path)
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}{path}", new { key = "invented-type", name = "Invented" });

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden,
            "the route is mapped — an unmapped one would be a 404 — and managing a catalogue "
            + "every listing references is closer to a schema change than to a moderation decision");
    }

    /// <summary>
    /// Inactive entries are hidden by default. A type retired from the platform must stop appearing
    /// in the picker without disappearing from the properties that already use it.
    /// </summary>
    [Fact]
    public async Task The_catalogue_hides_inactive_entries_unless_asked()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var active = await IdentityApiClient.ReadDataAsync<List<AccommodationTypeShape>>(
            await HttpClient.GetAsync($"{Root}/accommodation-types"));

        active.Should().OnlyContain(type => type.IsActive);

        var all = await IdentityApiClient.ReadDataAsync<List<AccommodationTypeShape>>(
            await HttpClient.GetAsync($"{Root}/accommodation-types?includeInactive=true"));

        all.Count.Should().BeGreaterThanOrEqualTo(active.Count);
    }

    // ── Discovery is open ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Search answers an anonymous caller.</b> Somebody deciding whether they can afford to move
    /// to a country needs to see what is available and what it costs before they join anything, and
    /// a housing platform whose front door required an account would be one nobody walks through.
    /// </summary>
    [Fact]
    public async Task Search_answers_without_a_token()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/search?countryCode=GB");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(response);

        results.Items.Should().NotBeNull();
        results.HasMore.Should().BeFalse("an empty corpus has no second page");
    }

    /// <summary>
    /// <b>The regression that cost this phase a full test run.</b> A GET route binding
    /// <c>[AsParameters]</c> over a request with collection properties throws at route-mapping time
    /// unless each is <c>string[]</c> and explicitly <c>[FromQuery]</c> — and it throws during host
    /// startup, so every test in the suite fails with a disposed service provider and none of them
    /// mentions housing. This is the assertion that would name it.
    /// </summary>
    [Fact]
    public async Task Search_binds_its_repeated_query_parameters()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync(
            $"{Root}/search"
            + "?accommodationTypes=apartment&accommodationTypes=studio"
            + "&amenities=wifi&amenities=kitchen"
            + "&leaseTypes=LongTerm"
            + "&landlordKinds=Individual");

        response.StatusCode.Should().Be(
            HttpStatusCode.OK,
            "repeated query parameters must bind rather than be inferred as a body");
    }

    [Fact]
    public async Task Search_reports_which_engine_answered()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(
            await HttpClient.GetAsync($"{Root}/search"));

        results.Engine.Should().Be(
            "ef",
            "the shipped engine names itself, so a deployment can tell whether the OpenSearch "
            + "adapter it thinks it installed is actually answering");
    }

    /// <summary>
    /// A page size beyond the module's ceiling is clamped rather than honoured. Without it a
    /// scripted client asks for a million and the server obliges.
    /// </summary>
    [Fact]
    public async Task Search_clamps_an_unreasonable_page_size()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/search?limit=5000");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var results = await IdentityApiClient.ReadDataAsync<SearchShape>(response);

        results.Items.Count.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public async Task A_property_that_does_not_exist_is_a_not_found_rather_than_a_failure()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/listings/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Neighbourhood discovery ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Neighbourhood discovery is open too, and it is the surface this module exists for.</b>
    /// "Where should I live" is asked long before "what can I rent", and it is answerable without
    /// an account.
    /// </summary>
    [Fact]
    public async Task Neighborhood_discovery_answers_without_a_token()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.GetAsync($"{Root}/neighborhoods?countryCode=GB");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var page = await IdentityApiClient.ReadDataAsync<List<JsonElement>>(response);

        page.Should().NotBeNull();
    }

    [Fact]
    public async Task An_unknown_neighborhood_is_a_not_found()
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"{Root}/neighborhoods/nowhere-at-all"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Everything else is shut ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The write surface refuses an anonymous caller. Each of these is a 401 rather than a 404,
    /// because pretending a route does not exist would leave a client unable to tell "sign in"
    /// from "this feature is gone".
    /// </summary>
    [Theory]
    [InlineData("/landlords")]
    [InlineData("/listings")]
    [InlineData("/neighborhoods/insights")]
    public async Task The_write_surface_refuses_an_anonymous_caller(string path)
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        var response = await HttpClient.PostAsync(
            $"{Root}{path}",
            new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("/listings/mine")]
    [InlineData("/dashboard")]
    [InlineData("/neighborhoods/dashboard")]
    public async Task The_authenticated_read_surface_refuses_an_anonymous_caller(string path)
    {
        Factory.EnsureDatabaseCreated();
        Api.ClearAuthentication();

        (await HttpClient.GetAsync($"{Root}{path}"))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The landlord dashboard and the landlord's own listing list carry the same permission, so
    /// they must answer the same caller the same way.
    /// <para>
    /// Asserted as an equality rather than as a status code, deliberately: what the platform grants
    /// a Verified member is the platform's business and may change, but two routes behind one
    /// permission diverging is always a defect — and it is the kind that shows up as "the dashboard
    /// says I have no properties" rather than as an error anybody investigates.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_landlord_dashboard_answers_the_same_caller_as_the_listing_list()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var dashboard = await HttpClient.GetAsync($"{Root}/dashboard");
        var listings = await HttpClient.GetAsync($"{Root}/listings/mine");

        dashboard.StatusCode.Should().Be(listings.StatusCode);

        dashboard.StatusCode.Should().NotBe(
            HttpStatusCode.NotFound, "the route is mapped");

        dashboard.StatusCode.Should().NotBe(
            HttpStatusCode.InternalServerError, "and it runs");
    }

    /// <summary>
    /// The editorial dashboard is staff work, and the gate is a role rather than a tier.
    /// </summary>
    [Fact]
    public async Task The_neighborhood_dashboard_refuses_a_verified_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        (await HttpClient.GetAsync($"{Root}/neighborhoods/dashboard"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The moderation queue refuses a member who is merely verified — the same rule the feature
    /// gate advertises, enforced where it actually matters.
    /// </summary>
    [Fact]
    public async Task The_moderation_queue_refuses_a_verified_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.GetAsync($"{Root}/moderation/reports");

        response.StatusCode.Should().Be(
            HttpStatusCode.Forbidden, "a gate a client greys out must also be shut on the server");
    }

    private static bool Enabled(FeatureAccessResponse features, string name) =>
        features.Features.Single(feature => feature.Name == name).IsEnabled;

    // ── Wire shapes, re-declared by hand ────────────────────────────────────────────────────

    private sealed record AccommodationTypeShape
    {
        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public bool IsShared { get; init; }

        public bool IsTemporary { get; init; }

        public int Order { get; init; }

        public bool IsActive { get; init; }
    }

    private sealed record AmenityShape
    {
        public string Key { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string? Group { get; init; }

        public bool IsAccessibilityFeature { get; init; }

        public bool IsFilterable { get; init; }

        public bool IsActive { get; init; }
    }

    private sealed record SearchShape
    {
        public List<JsonElement> Items { get; init; } = [];

        public string? NextCursor { get; init; }

        public bool HasMore { get; init; }

        public string? Engine { get; init; }
    }
}
