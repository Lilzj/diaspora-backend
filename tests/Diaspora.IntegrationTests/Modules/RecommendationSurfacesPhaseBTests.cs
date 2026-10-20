using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Recommendations phase B over real HTTP: the monetization surface (MON-GAP-011) and community
/// suggestions by presence country.
/// <para>
/// <b>Shared database.</b> Every assertion is about rows this test created — a listing it projected
/// for a member it registered, communities present in a country chosen per test — so nothing here
/// depends on what else the suite has written, and no list is assumed to be complete.
/// </para>
/// </summary>
[Collection(DiscoveryCollection.Name)]
public sealed class RecommendationSurfacesPhaseBTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/recommendations";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RecommendationSurfacesPhaseBTests(CustomWebApplicationFactory factory) : base(factory) =>
        factory.EnsureDatabaseCreated();

    // ── Monetization ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Monetization_recommendations_refuse_an_anonymous_caller()
    {
        var response = await HttpClient.GetAsync(new Uri($"{Root}/monetization", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Monetization_recommendations_refuse_a_malformed_country()
    {
        await MemberAsync();

        var response = await HttpClient.GetAsync(
            new Uri($"{Root}/monetization?country=GBR", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_member_with_nothing_of_their_own_is_offered_no_promotion()
    {
        await MemberAsync();

        var items = await GetMonetizationAsync();

        items.Should().NotContain(item => item.Kind == "product",
            "a promotion is for something the member owns, and this member owns nothing");
    }

    /// <summary>
    /// The member's own live listing, with nothing promoting it, is offered a promotion the
    /// catalogue sells for listings — with a reason that names the listing and a route that opens
    /// the promote screen on it.
    /// </summary>
    [Fact]
    public async Task An_unpromoted_listing_the_member_owns_is_offered_a_promotion()
    {
        var userId = await MemberAsync();
        var listingId = await ProjectOwnedAsync(userId, RecommendationTargetType.MarketplaceListing, "Teak dining table");

        var items = await GetMonetizationAsync();

        var item = items.Should().ContainSingle(entry => entry.SubjectId == listingId).Subject;

        item.Kind.Should().Be("product");
        item.SubjectType.Should().Be("MarketplaceListing");
        item.Code.Should().StartWith("marketplace.listing.");
        item.ReasonCode.Should().Be("subject.unpromoted");
        item.Reason.Should().Contain("“Teak dining table”");
        item.Route.Should().Contain($"subjectId={listingId}").And.Contain($"productCode={item.Code}");
        item.TargetType.Should().Be("MonetizationOffer");
        item.Surface.Should().Be("Monetization");
        item.TargetId.ToString("D").Should().Be(item.RecommendationId);
        item.SetId.Should().NotBeNull("what was served is recorded so feedback can be attributed");
        item.Confidence.Should().BeInRange(0m, 1m);
    }

    /// <summary>Somebody else's listing is never offered to this member, however unpromoted.</summary>
    [Fact]
    public async Task Another_members_listing_is_never_offered()
    {
        var otherId = Guid.NewGuid();
        var theirs = await ProjectOwnedAsync(otherId, RecommendationTargetType.MarketplaceListing, "Not yours");

        await MemberAsync();

        (await GetMonetizationAsync()).Should().NotContain(item => item.SubjectId == theirs);
    }

    /// <summary>
    /// <b>Dismissal goes through the ordinary feedback route</b> with the target, surface and set the
    /// suggestion carried, and the suggestion does not come back.
    /// </summary>
    [Fact]
    public async Task A_dismissed_monetization_suggestion_does_not_come_back()
    {
        var userId = await MemberAsync();
        var listingId = await ProjectOwnedAsync(userId, RecommendationTargetType.MarketplaceListing, "Bicycle");

        var item = (await GetMonetizationAsync()).Single(entry => entry.SubjectId == listingId);

        var dismissed = await HttpClient.PostAsJsonAsync(
            new Uri($"{Root}/dismiss", UriKind.Relative),
            new
            {
                kind = "Dismissed",
                targetType = item.TargetType,
                targetId = item.TargetId,
                surface = item.Surface,
                setId = item.SetId,
                position = item.Position
            });

        dismissed.StatusCode.Should().Be(HttpStatusCode.OK);

        var feedback = await ReadDataAsync<FeedbackShape>(dismissed);
        feedback.WasRecorded.Should().BeTrue();
        feedback.SuppressedUntil.Should().NotBeNull();

        (await GetMonetizationAsync()).Should().NotContain(entry => entry.SubjectId == listingId);
    }

    /// <summary>
    /// <b>Never what they already hold.</b> Once the member has bought a promotion for the listing —
    /// even one still awaiting its payment — no promotion is suggested for it.
    /// </summary>
    [Fact]
    public async Task A_listing_the_member_has_bought_a_promotion_for_is_not_offered_another()
    {
        var userId = await MemberAsync();
        await ActivateFinancialIdentityAsync(userId);

        var listingId = await ProjectOwnedAsync(userId, RecommendationTargetType.MarketplaceListing, "Armchair");

        var offered = (await GetMonetizationAsync()).Single(entry => entry.SubjectId == listingId);

        var purchase = await HttpClient.PostAsJsonAsync(
            new Uri("/api/v1/financial/purchases", UriKind.Relative),
            new
            {
                productCode = offered.Code,
                subjectType = "MarketplaceListing",
                subjectId = listingId,
                methodKind = "Card",
                idempotencyKey = Guid.NewGuid().ToString("N")
            });

        purchase.StatusCode.Should().Be(
            HttpStatusCode.OK, "the suggested product must be one the purchase route accepts: {0}",
            await purchase.Content.ReadAsStringAsync());

        (await GetMonetizationAsync()).Should().NotContain(entry => entry.SubjectId == listingId);
    }

    /// <summary>
    /// A member running a provider profile is offered the provider plan, and the reason names the
    /// profile; the free plan is never offered.
    /// </summary>
    [Fact]
    public async Task A_member_running_a_provider_profile_is_offered_the_provider_plan()
    {
        var userId = await MemberAsync();
        await ProjectOwnedAsync(userId, RecommendationTargetType.Provider, "Ada's Tax Clinic");

        var items = await GetMonetizationAsync();

        var plan = items.Should().ContainSingle(item => item.Kind == "plan" && item.Code == "provider.monthly").Subject;

        plan.ReasonCode.Should().Be("plan.matches_provider_role");
        plan.Reason.Should().Contain("“Ada's Tax Clinic”");
        plan.Route.Should().Be("/monetization/plans?highlight=provider.monthly");
        items.Should().NotContain(item => item.Code == "free");
    }

    /// <summary>The literal under the generic surface route answers the same, not the engine's empty page.</summary>
    [Fact]
    public async Task The_generic_surface_route_serves_monetization_too()
    {
        var userId = await MemberAsync();
        var listingId = await ProjectOwnedAsync(userId, RecommendationTargetType.HousingListing, "Two-bed flat");

        var response = await HttpClient.GetAsync(new Uri($"{Root}/surface/monetization", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        (await ReadDataAsync<List<MonetizationShape>>(response))
            .Should().Contain(item => item.SubjectId == listingId && item.SubjectType == "HousingListing");
    }

    // ── Communities by presence country ───────────────────────────────────────────────────────

    [Fact]
    public async Task Community_suggestions_refuse_an_anonymous_caller()
    {
        var response = await HttpClient.GetAsync(new Uri($"{Root}/communities", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Community_suggestions_refuse_a_malformed_country()
    {
        await MemberAsync();

        var response = await HttpClient.GetAsync(new Uri($"{Root}/communities?country=G1", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// <b>The country really decides the candidates.</b> Asking for one presence country returns the
    /// community present there and not the one present elsewhere, and asking for the other reverses
    /// it.
    /// </summary>
    [Fact]
    public async Task The_presence_country_decides_which_communities_are_suggested()
    {
        await MemberAsync();

        var inTuvalu = await SeedCommunityAsync("Presence probe TV", presence: ["TV"]);
        var inNauru = await SeedCommunityAsync("Presence probe NR", presence: ["NR"]);

        var tuvalu = await GetCommunitiesAsync("TV");

        var suggested = tuvalu.Items.Should().ContainSingle(item => item.TargetId == inTuvalu).Subject;
        suggested.TargetType.Should().Be("Community");
        suggested.Reason.Code.Should().Be("PresentInCountry");
        suggested.Reason.Text.Should().Be("Active in TV.");
        suggested.Location.CountryCode.Should().Be("TV");
        tuvalu.Items.Should().NotContain(item => item.TargetId == inNauru);
        tuvalu.Surface.Should().Be("Communities");
        tuvalu.SetId.Should().NotBeNull();

        var nauru = await GetCommunitiesAsync("nr");

        nauru.Items.Should().Contain(item => item.TargetId == inNauru);
        nauru.Items.Should().NotContain(item => item.TargetId == inTuvalu);
    }

    [Fact]
    public async Task A_community_the_member_already_belongs_to_is_not_suggested()
    {
        var userId = await MemberAsync();

        var joined = await SeedCommunityAsync("Joined probe TV", presence: ["TV"], member: UserId.From(userId));
        var open = await SeedCommunityAsync("Open probe TV", presence: ["TV"]);

        var page = await GetCommunitiesAsync("TV");

        page.Items.Should().Contain(item => item.TargetId == open);
        page.Items.Should().NotContain(item => item.TargetId == joined);
    }

    [Fact]
    public async Task A_dismissed_community_is_not_suggested_again()
    {
        await MemberAsync();

        var community = await SeedCommunityAsync("Dismiss probe TV", presence: ["TV"]);

        var page = await GetCommunitiesAsync("TV");
        var item = page.Items.Single(entry => entry.TargetId == community);

        var dismissed = await HttpClient.PostAsJsonAsync(
            new Uri($"{Root}/dismiss", UriKind.Relative),
            new
            {
                kind = "Dismissed",
                targetType = "Community",
                targetId = community,
                surface = "Communities",
                setId = page.SetId,
                position = item.Position
            });

        dismissed.StatusCode.Should().Be(HttpStatusCode.OK);

        (await GetCommunitiesAsync("TV")).Items.Should().NotContain(entry => entry.TargetId == community);
    }

    [Fact]
    public async Task A_suspended_or_private_community_is_never_suggested()
    {
        await MemberAsync();

        var suspended = await SeedCommunityAsync("Suspended probe TV", presence: ["TV"], suspend: true);
        var hidden = await SeedCommunityAsync(
            "Private probe TV", presence: ["TV"], visibility: CommunityVisibility.Private);

        var page = await GetCommunitiesAsync("TV");

        page.Items.Should().NotContain(item => item.TargetId == suspended || item.TargetId == hidden);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    private async Task<Guid> MemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        Api.Authenticate(auth.Tokens.AccessToken);

        return auth.User.Id;
    }

    private async Task ActivateFinancialIdentityAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var identities = scope.ServiceProvider.GetRequiredService<FinancialIdentityService>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var account = await accounts.FindByOwnerAsync(userId, FinancialAccountType.Personal)
            ?? await identities.GetOrCreateAsync(userId, FinancialAccountType.Personal, "GBP");

        await identities.ActivateAsync(account);
        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Projects a live item owned by a member, through the repository the consumers write with.
    /// <para>
    /// The publication flows of four verticals are exercised in their own suites; what this surface
    /// reads is the projection, so the projection is what is seeded.
    /// </para>
    /// </summary>
    private async Task<Guid> ProjectOwnedAsync(Guid ownerId, RecommendationTargetType type, string title)
    {
        using var scope = Factory.Services.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IRecommendationCandidateRepository>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var targetId = Guid.NewGuid();

        var candidate = await repository.GetOrProjectAsync(type, targetId, "Tests", title, CancellationToken.None);

        candidate.Place("GB");
        candidate.Attribute(UserId.From(ownerId));
        candidate.Restrict(CandidateVisibility.Members);
        candidate.Schedule(clock.UtcNow.AddDays(-3), clock.UtcNow.AddDays(-3));

        repository.Update(candidate);
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        return targetId;
    }

    private async Task<Guid> SeedCommunityAsync(
        string name,
        IReadOnlyList<string> presence,
        UserId? member = null,
        bool suspend = false,
        CommunityVisibility visibility = CommunityVisibility.Public)
    {
        var slug = CommunitySlug.FromName($"{name}-{Guid.NewGuid():N}").Value;

        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var memberships = scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = CommunityAggregate
            .Propose(CommunityId.New(), slug, name, CommunityType.NationalDiaspora, visibility, "NG")
            .Value;

        community.Open();
        community.SetPresence(presence);

        if (member is not null)
        {
            var membership = CommunityMembership.Start(
                CommunityMembershipId.New(), community.Id, member, CommunityMembershipState.Following).Value;

            membership.Join(community, requiresApproval: false, DateTimeOffset.UtcNow);
            community.RecordMemberJoined();
            memberships.Add(membership);
        }

        if (suspend)
        {
            community.Suspend("Suspended by the test.");
        }

        communities.Add(community);
        await unitOfWork.SaveChangesAsync();

        return community.Id.Value;
    }

    private async Task<List<MonetizationShape>> GetMonetizationAsync()
    {
        var response = await HttpClient.GetAsync(new Uri($"{Root}/monetization?limit=50", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<List<MonetizationShape>>(response);
    }

    private async Task<PageShape> GetCommunitiesAsync(string country)
    {
        var response = await HttpClient.GetAsync(
            new Uri($"{Root}/communities?country={country}&limit=100", UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        return await ReadDataAsync<PageShape>(response);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

        return payload.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record MonetizationShape(
        string RecommendationId,
        string Kind,
        string Code,
        string Title,
        string Reason,
        string ReasonCode,
        decimal Confidence,
        string Route,
        string? SubjectType,
        Guid? SubjectId,
        string TargetType,
        Guid TargetId,
        string Surface,
        Guid? SetId,
        int Position);

    private sealed record FeedbackShape(Guid FeedbackId, string Kind, DateTimeOffset? SuppressedUntil, bool WasRecorded);

    private sealed record ReasonShape(string Code, string Text);

    private sealed record LocationShape(string? CountryCode);

    private sealed record ItemShape(
        string TargetType, Guid TargetId, string Title, int Position, ReasonShape Reason, LocationShape Location);

    private sealed record PageShape(List<ItemShape> Items, Guid? SetId, string Surface);
}
