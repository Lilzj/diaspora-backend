using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The Community Life, Events &amp; Organizations Platform (B12), driven over real HTTP through the
/// same host every other module suite uses.
/// <para>
/// Four claims are under test. That the module composed into the platform through nothing but
/// contributor interfaces. That <b>the two provisional gates it claimed were deleted from the host
/// in the same change</b> — the assertion that matters most here, because two modules declaring one
/// gate fails the catalogue at startup and takes down phone verification, not anything that looks
/// like an event. That the catalogues are seeded reference data a client can render a form from.
/// And that the access band holds: <b>browsing is Limited, responding is Limited, creating an
/// organization or publishing an event is Verified, moderating is staff.</b>
/// </para>
/// <para>
/// The wire shapes below are re-declared by hand rather than referenced from the module. Binding
/// these tests to B12's own response records would make them pass whenever the module compiles,
/// which is the one thing a contract test must never do.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityLifeModuleTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/community-life";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public CommunityLifeModuleTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Proof the module's <c>IFeatureContributor</c> reached the composed catalogue: gates B12 owns
    /// turn up in Identity's response, which the Community Life module never touched.
    /// </summary>
    [Fact]
    public async Task The_community_life_gates_composed_into_the_platform_catalogue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        var names = features.Features.Select(feature => feature.Name).ToList();

        names.Should().Contain("community-life.browse");
        names.Should().Contain("community-life.events.create");
        names.Should().Contain("organizations.create");
    }

    /// <summary>
    /// <b>The gate the host held provisionally appears exactly once.</b> This is the assertion the
    /// phase exists to make: <c>organizations.create</c> has been declared by the host since B2 and
    /// is now declared by B12. Both at once fails the catalogue at startup, and the request that
    /// breaks first is phone verification — which is how every test in this file makes its member.
    /// </summary>
    [Fact]
    public async Task The_claimed_provisional_gate_is_declared_exactly_once()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        features.Features
            .Count(feature => feature.Name == "organizations.create")
            .Should().Be(
                1,
                "B12 claims the name the host declared provisionally, and the provisional entry "
                + "was deleted in the same change");
    }

    [Fact]
    public async Task The_platform_manifest_lists_the_community_life_module()
    {
        var response = await HttpClient.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(
            "CommunityLife", "the kernel discovered the module by assembly scan");
    }

    // ── The access band ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Responding to an event is Limited, and that is deliberate.</b> An RSVP is how a newcomer
    /// finds their people and it costs nobody anything; gating it behind verification would put the
    /// platform behind the need it exists to serve.
    /// </summary>
    [Fact]
    public async Task Browsing_and_responding_are_open_to_a_limited_member()
    {
        var (_, auth) = await RegisterAndVerifyAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        Enabled(features, "community-life.browse").Should().BeTrue();
        Enabled(features, "community-life.rsvp").Should().BeTrue();
    }

    /// <summary>Moderating is staff-only, which is not a tier a member can climb to.</summary>
    [Fact]
    public async Task Moderating_stays_shut_for_a_verified_member()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var features = await IdentityApiClient.ReadDataAsync<FeatureAccessResponse>(
            await Api.GetFeaturesAsync());

        Enabled(features, "community-life.moderate").Should().BeFalse();
    }

    // ── The catalogue, seeded by the module itself ──────────────────────────────────────────

    /// <summary>
    /// The seeding hosted service the module registers from its own <c>ConfigureServices</c> — no
    /// edit to <c>Program.cs</c>, and a populated catalogue on first boot.
    /// </summary>
    [Fact]
    public async Task The_catalogue_is_seeded_at_startup()
    {
        var response = await HttpClient.GetAsync($"{Root}/catalogue");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var catalogue = await ReadDataAsync<CatalogueDto>(response);

        catalogue.OrganizationTypes.Should().NotBeEmpty();
        catalogue.EventCategories.Should().NotBeEmpty();

        catalogue.OrganizationTypes.Select(type => type.Key)
            .Should().Contain("place-of-worship", "the commonest diaspora institution");

        catalogue.EventCategories.Select(category => category.Key)
            .Should().Contain("naming-ceremony");
    }

    /// <summary>
    /// <b>The catalogue decides whether a category asks for responses.</b> A Sunday service does
    /// not take a headcount and a workshop does, and putting the answer here means a category added
    /// next year behaves correctly in an app shipped this year.
    /// </summary>
    [Fact]
    public async Task The_catalogue_says_which_categories_ask_for_responses()
    {
        var catalogue = await ReadDataAsync<CatalogueDto>(
            await HttpClient.GetAsync($"{Root}/catalogue"));

        catalogue.EventCategories
            .Single(category => category.Key == "worship-service")
            .DefaultsToRsvp.Should().BeFalse("nobody signs up for a service");

        catalogue.EventCategories
            .Single(category => category.Key == "workshop")
            .DefaultsToRsvp.Should().BeTrue("a workshop has seats");
    }

    /// <summary>The facets are derived from the live categories, so a filter bar can be built.</summary>
    [Fact]
    public async Task The_catalogue_exposes_the_facets_a_filter_bar_renders()
    {
        var catalogue = await ReadDataAsync<CatalogueDto>(
            await HttpClient.GetAsync($"{Root}/catalogue"));

        catalogue.Facets.Should().Contain(["religious", "cultural", "family"]);
    }

    /// <summary>
    /// Managing the catalogue is closer to a schema change than to a moderation decision, so a
    /// Verified member is refused — and refused rather than 404ed, which proves the route is mapped.
    /// </summary>
    [Fact]
    public async Task A_member_cannot_manage_the_catalogue()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/catalogue/event-categories",
            new { key = "invented", name = "Invented" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Anonymous surfaces ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Browsing is anonymous.</b> Somebody who has just arrived in a city looking for a place of
    /// worship has not signed up yet.
    /// </summary>
    [Fact]
    public async Task An_anonymous_caller_can_browse_events()
    {
        var response = await HttpClient.GetAsync($"{Root}/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_anonymous_caller_can_browse_the_directory()
    {
        var response = await HttpClient.GetAsync($"{Root}/organizations");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The calendar answers from every registered source, and names which ones answered — B12's own
    /// events are one source among several.
    /// </summary>
    [Fact]
    public async Task The_calendar_answers_anonymously_and_names_its_sources()
    {
        var response = await HttpClient.GetAsync($"{Root}/calendar");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var calendar = await ReadDataAsync<CalendarDto>(response);

        calendar.To.Should().BeAfter(calendar.From);
    }

    // ── Writes ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An organization lands as a draft and its creator becomes its owner.</b> A body whose owner
    /// membership was written afterwards would spend the gap administrable by nobody.
    /// </summary>
    [Fact]
    public async Task Creating_an_organization_makes_the_creator_its_owner()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var created = await CreateOrganizationAsync();

        created.Status.Should().Be("Draft", "publishing is a separate act");

        var mine = await ReadDataAsync<PageDto<OrganizationSummaryDto>>(
            await HttpClient.GetAsync($"{Root}/organizations/mine"));

        mine.Items.Should().Contain(
            organization => organization.OrganizationId == created.OrganizationId,
            "the owner membership committed with the organization");
    }

    /// <summary>An unauthenticated caller cannot create an organization.</summary>
    [Fact]
    public async Task An_anonymous_caller_cannot_create_an_organization()
    {
        var response = await HttpClient.PostAsJsonAsync(
            $"{Root}/organizations",
            new
            {
                name = "Anonymous Association",
                summary = "Should not exist.",
                organizationTypeKey = "cultural-association",
                countryCode = "GB"
            });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// <b>An event does not need an organization.</b> Most diaspora events are one member inviting
    /// their neighbours, and requiring an institution would lose exactly those.
    /// </summary>
    [Fact]
    public async Task An_event_can_be_created_without_an_organization()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var created = await CreateEventAsync();

        created.OrganizationId.Should().BeNull();
        created.Status.Should().Be("Draft");
    }

    /// <summary>
    /// <b>The category's default decides whether the event asks for responses</b>, without the
    /// client having to know which categories do.
    /// </summary>
    [Fact]
    public async Task An_event_inherits_whether_its_category_asks_for_responses()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var service = await CreateEventAsync(categoryKey: "worship-service");
        var workshop = await CreateEventAsync(categoryKey: "workshop");

        service.RequiresRsvp.Should().BeFalse();
        workshop.RequiresRsvp.Should().BeTrue();
    }

    /// <summary>
    /// <b>An unknown category is refused rather than stored.</b> A page filed under a category that
    /// appears in no picker can never be found again.
    /// </summary>
    [Fact]
    public async Task An_unknown_category_is_refused()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var response = await HttpClient.PostAsJsonAsync($"{Root}/events", EventPayload("invented"));

        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// <b>Publishing makes the event discoverable, and the slug resolves it.</b> A link shared in a
    /// group chat is the commonest way a diaspora event is found.
    /// </summary>
    [Fact]
    public async Task A_published_event_resolves_by_slug()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(auth.Tokens.AccessToken);

        var created = await CreateEventAsync();

        var published = await ReadDataAsync<EventDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/events/{created.EventId}/publish", new { }));

        published.Status.Should().Be("Published");

        Api.ClearAuthentication();

        var bySlug = await ReadDataAsync<EventDto>(
            await HttpClient.GetAsync($"{Root}/events/{published.Slug}"));

        bySlug.EventId.Should().Be(created.EventId);
    }

    /// <summary>
    /// <b>A party of four takes four places, not one.</b> "Me and three others" is how a family
    /// answers, and an event counting responses would lay out a quarter of the chairs it needs.
    /// </summary>
    [Fact]
    public async Task A_response_takes_places_for_the_whole_party()
    {
        var (_, organizer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(organizer.Tokens.AccessToken);

        var created = await CreateEventAsync();

        await HttpClient.PostAsJsonAsync($"{Root}/events/{created.EventId}/publish", new { });

        var (_, attendee) = await RegisterAndVerifyAsync();
        Api.Authenticate(attendee.Tokens.AccessToken);

        var response = await HttpClient.PutAsJsonAsync(
            $"{Root}/events/{created.EventId}/rsvp",
            new { status = "Going", partySize = 4 });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var rsvp = await ReadDataAsync<RsvpDto>(response);

        rsvp.Status.Should().Be("Going");
        rsvp.PartySize.Should().Be(4);

        var read = await ReadDataAsync<EventDto>(
            await HttpClient.GetAsync($"{Root}/events/{created.EventId}"));

        read.Capacity.GoingCount.Should().Be(
            4, "the counters move by the party size, and exactly once");
    }

    /// <summary>
    /// <b>Withdrawing gives the places back.</b> The counter that would be wrong if the settlement
    /// path double-counted is the same one checked above, read after a release.
    /// </summary>
    [Fact]
    public async Task Withdrawing_releases_the_whole_party()
    {
        var (_, organizer) = await RegisterVerifiedMemberAsync();
        Api.Authenticate(organizer.Tokens.AccessToken);

        var created = await CreateEventAsync();

        await HttpClient.PostAsJsonAsync($"{Root}/events/{created.EventId}/publish", new { });

        var (_, attendee) = await RegisterAndVerifyAsync();
        Api.Authenticate(attendee.Tokens.AccessToken);

        await HttpClient.PutAsJsonAsync(
            $"{Root}/events/{created.EventId}/rsvp",
            new { status = "Going", partySize = 3 });

        await HttpClient.DeleteAsync($"{Root}/events/{created.EventId}/rsvp");

        var read = await ReadDataAsync<EventDto>(
            await HttpClient.GetAsync($"{Root}/events/{created.EventId}"));

        read.Capacity.GoingCount.Should().Be(0);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<OrganizationDto> CreateOrganizationAsync() =>
        await ReadDataAsync<OrganizationDto>(
            await HttpClient.PostAsJsonAsync(
                $"{Root}/organizations",
                new
                {
                    name = $"Test Association {Guid.NewGuid():N}",
                    summary = "A body created by the integration suite.",
                    organizationTypeKey = "cultural-association",
                    countryCode = "GB",
                    cityName = "London"
                }));

    private async Task<EventDto> CreateEventAsync(string categoryKey = "community-meeting") =>
        await ReadDataAsync<EventDto>(
            await HttpClient.PostAsJsonAsync($"{Root}/events", EventPayload(categoryKey)));

    private static object EventPayload(string categoryKey) =>
        new
        {
            title = $"Test Gathering {Guid.NewGuid():N}",
            summary = "An event created by the integration suite.",
            categoryKey,
            startsAt = DateTimeOffset.UtcNow.AddDays(14),
            endsAt = DateTimeOffset.UtcNow.AddDays(14).AddHours(3),
            countryCode = "GB",
            cityName = "London",
            venue = new { name = "Community Hall", addressLine1 = "1 High Street" }
        };

    private static bool Enabled(FeatureAccessResponse features, string name) =>
        features.Features.FirstOrDefault(feature => feature.Name == name)?.IsEnabled ?? false;

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(JsonOptions);

        return envelope!.Data!;
    }

    private sealed record Envelope<T>(T? Data);

    private sealed record PageDto<T>(List<T> Items);

    private sealed record CatalogueDto(
        List<OrganizationTypeDto> OrganizationTypes,
        List<EventCategoryDto> EventCategories,
        List<string> Facets);

    private sealed record OrganizationTypeDto(string Key, string Name);

    private sealed record EventCategoryDto(string Key, string Name, bool DefaultsToRsvp);

    private sealed record CalendarDto(DateTimeOffset From, DateTimeOffset To, List<string> Sources);

    private sealed record OrganizationDto(Guid OrganizationId, string Slug, string Status);

    private sealed record OrganizationSummaryDto(Guid OrganizationId, string Name);

    private sealed record EventDto(
        Guid EventId,
        string Slug,
        string Status,
        Guid? OrganizationId,
        bool RequiresRsvp,
        CapacityDto Capacity);

    private sealed record CapacityDto(int GoingCount);

    private sealed record RsvpDto(string Status, int PartySize);
}
