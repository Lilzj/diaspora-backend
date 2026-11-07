using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.IntegrationTests.Common;
using FluentAssertions;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// COMMUNITY-LIFE-GAP-006, over real HTTP.
/// <para>
/// <b>The load-bearing assertion is that a campaign cannot collect before it has a cause, and that
/// publishing creates one.</b> Everything else on the fundraising surface follows from it: no cause,
/// no <c>canDonate</c>, no donate button, no money outside B15.
/// </para>
/// <para>
/// <b>Every figure printed here comes from a real donation.</b> The totals are the cause's, moved by
/// <c>POST /financial/donations</c> and a real capture — nothing seeds a raised amount, because a
/// test that did would prove the surface reads a column rather than that it reads the money.
/// </para>
/// </summary>
public sealed class CampaignLifecycleTests : CampaignTestBase<CustomWebApplicationFactory>
{
    public CampaignLifecycleTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    // ── Creation ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A campaign opens in draft, with no cause and no way to collect.</summary>
    [Fact]
    public async Task A_new_campaign_is_a_draft_with_no_cause()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        campaign.Status.Should().Be("Draft");
        campaign.CauseCode.Should().BeNull();
        campaign.CanDonate.Should().BeFalse();
        campaign.CanManage.Should().BeTrue();
        campaign.Progress.Raised.MinorUnits.Should().Be(0);
        campaign.Progress.Percentage.Should().Be(0);
        campaign.Category.Should().Be("Repatriation");
    }

    /// <summary>
    /// The client's own category spelling survives the round trip.
    /// <para>
    /// Three of the eleven carry a space and a C# enum member cannot; returning
    /// <c>CommunityProject</c> would be a value no branch of the client matches, and it would fail
    /// silently as an unstyled chip rather than as an error.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_category_with_a_space_survives_the_round_trip()
    {
        await MemberAsync();

        var response = await HttpClient.PostAsJsonAsync(
            new Uri($"{Communities}/campaigns", UriKind.Relative),
            new
            {
                title = "New roof for the hall",
                summary = "A short summary of why.",
                story = "The whole story, at length.",
                category = "Community Project",
                targetAmount = 100m,
                currency = "GBP",
                beneficiaryType = "Community",
                beneficiaryName = "The hall committee",
                acceptedTerms = true
            });

        (await ReadDataAsync<CampaignShape>(response)).Category.Should().Be("Community Project");
    }

    /// <summary>
    /// Terms acceptance is refused server-side, not only in the form.
    /// <para>
    /// The register asks for exactly this, and a client that stopped sending the flag would otherwise
    /// create campaigns nobody had agreed terms for.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_campaign_cannot_be_created_without_accepting_the_terms()
    {
        await MemberAsync();

        (await PostCampaignAsync(acceptedTerms: false)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Creating a campaign needs a verified account. The giving bar, inherited.</summary>
    [Fact]
    public async Task An_unverified_member_cannot_create_a_campaign()
    {
        var (_, auth) = await RegisterAndVerifyAsync();

        Api.Authenticate(auth.Tokens.AccessToken);
        Authorize(auth.Tokens.AccessToken);

        (await PostCampaignAsync()).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>An anonymous caller creates nothing.</summary>
    [Fact]
    public async Task Creating_a_campaign_requires_authentication()
    {
        HttpClient.DefaultRequestHeaders.Authorization = null;

        (await PostCampaignAsync()).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A retried creation returns the campaign it already made.
    /// <para>
    /// B15's idempotency primitive, copied rather than reinvented — the same shape payment intents,
    /// purchases, subscriptions, donations and contributions already use.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Creating_a_campaign_twice_with_one_key_makes_one_campaign()
    {
        await MemberAsync();

        var key = Guid.NewGuid().ToString("N");

        var first = await ReadDataAsync<CampaignShape>(await PostCampaignAsync(idempotencyKey: key));
        var second = await ReadDataAsync<CampaignShape>(await PostCampaignAsync(idempotencyKey: key));

        second.CampaignId.Should().Be(first.CampaignId);

        var mine = await SearchAsync("mine=true");

        mine.Items.Count(campaign => campaign.CampaignId == first.CampaignId).Should().Be(1);
    }

    // ── The publishing invariant ─────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Publishing creates the giving cause, and the campaign can collect the moment it does.</b>
    /// The two happen in one unit of work, so there is no instant at which a published campaign has
    /// nowhere to collect.
    /// </summary>
    [Fact]
    public async Task Publishing_creates_the_donation_cause()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        campaign.Status.Should().Be("Published");
        campaign.CauseCode.Should().NotBeNullOrWhiteSpace();
        campaign.CanDonate.Should().BeTrue();

        // The cause is a real B15 cause, on B15's own certified route.
        var causes = await ReadDataAsync<List<CauseShape>>(
            await HttpClient.GetAsync(new Uri($"{Financial}/donations/causes", UriKind.Relative)));

        causes.Should().Contain(cause => cause.Code == campaign.CauseCode);
    }

    /// <summary>
    /// Money given to a published campaign is a real B15 donation, and the campaign's totals are the
    /// cause's.
    /// </summary>
    [Fact]
    public async Task A_donation_to_a_published_campaign_moves_the_campaigns_totals()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await DonateAsync(campaign.CauseCode!, 30m);

        var refreshed = await GetCampaignAsync(campaign.CampaignId);

        refreshed.Progress.Raised.MinorUnits.Should().Be(3_000);
        refreshed.Progress.SupporterCount.Should().Be(1);
        refreshed.Progress.Percentage.Should().Be(30, "the target is £100 and the server computes it");
    }

    /// <summary>A campaign in draft has no cause to donate to, and says so.</summary>
    [Fact]
    public async Task A_draft_campaign_has_nothing_to_donate_to()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        campaign.CauseCode.Should().BeNull();
        campaign.CanDonate.Should().BeFalse();
    }

    // ── The state machine ────────────────────────────────────────────────────────────────────

    /// <summary>Pausing closes the cause, so a paused campaign takes no money.</summary>
    [Fact]
    public async Task Pausing_a_campaign_stops_it_collecting()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        var paused = await ReadDataAsync<CampaignShape>(
            await TransitionAsync(campaign.CampaignId, "pause"));

        paused.Status.Should().Be("Paused");
        paused.CanDonate.Should().BeFalse();

        (await PostDonationAsync(campaign.CauseCode!, 10m)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Resuming reopens it, and the money can flow again.</summary>
    [Fact]
    public async Task Resuming_a_paused_campaign_lets_it_collect_again()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await TransitionAsync(campaign.CampaignId, "pause");

        var resumed = await ReadDataAsync<CampaignShape>(
            await TransitionAsync(campaign.CampaignId, "resume"));

        resumed.Status.Should().Be("Active");
        resumed.CanDonate.Should().BeTrue();

        (await PostDonationAsync(campaign.CauseCode!, 10m)).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>An illegal move answers 409, exactly as the register asks.</summary>
    [Fact]
    public async Task An_illegal_transition_answers_409()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        // Draft cannot be paused. The table says so, and the server decides rather than the client.
        (await TransitionAsync(campaign.CampaignId, "pause")).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        (await GetCampaignAsync(campaign.CampaignId)).Status.Should().Be("Draft");
    }

    /// <summary>Nothing moves out of a terminal state, however many times it is asked.</summary>
    [Fact]
    public async Task A_cancelled_campaign_cannot_be_revived()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        (await TransitionAsync(campaign.CampaignId, "cancel")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        foreach (var verb in new[] { "submit", "publish", "resume", "pause", "close" })
        {
            (await TransitionAsync(campaign.CampaignId, verb)).StatusCode
                .Should().Be(HttpStatusCode.Conflict, "a cancelled campaign cannot {0}", verb);
        }
    }

    /// <summary>A repeated transition to the state it already holds is a no-op, not an error.</summary>
    [Fact]
    public async Task Repeating_a_transition_is_safe()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        (await TransitionAsync(campaign.CampaignId, "pause")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await TransitionAsync(campaign.CampaignId, "pause")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await GetCampaignAsync(campaign.CampaignId)).Status.Should().Be("Paused");
    }

    /// <summary>
    /// The server tells the caller which moves it would accept.
    /// <para>
    /// The client's own table decides which buttons to draw; this is the server's answer, and it is
    /// empty for somebody who does not manage the campaign.
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_available_transitions_are_the_servers_answer()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        campaign.AvailableTransitions.Should().BeEquivalentTo(
            ["Active", "Paused", "Cancelled", "Closed"]);

        await MemberAsync();

        (await GetCampaignAsync(campaign.CampaignId)).AvailableTransitions.Should().BeEmpty();
    }

    /// <summary>Somebody else's campaign is not yours to move.</summary>
    [Fact]
    public async Task A_stranger_cannot_drive_someone_elses_campaign()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        await MemberAsync();

        (await TransitionAsync(campaign.CampaignId, "pause")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        (await HttpClient.PutAsJsonAsync(
                new Uri($"{Communities}/campaigns/{campaign.CampaignId}", UriKind.Relative),
                new { title = "Mine now" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Staff (7 Nov 2026) ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>Staff are refused on the organiser's own routes, and the organiser is not.</b>
    /// <para>
    /// Until 7 Nov 2026 holding <c>financial:giving:manage</c> skipped the organiser check, so a
    /// moderator could pause, close or cancel anybody's fundraiser with no reason recorded. The
    /// owner's rule is that staff act only through moderation routes.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("Moderator")]
    [InlineData("Admin")]
    public async Task Staff_cannot_edit_or_move_a_members_campaign_through_the_organisers_routes(
        string role)
    {
        var (_, organiser) = await SignedInMemberAsync();

        var campaign = await PublishedCampaignAsync();

        await SignInAsRoleAsync(role);

        (await EditAsync(campaign.CampaignId, "Edited by staff")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        foreach (var verb in new[] { "pause", "close", "cancel" })
        {
            (await TransitionAsync(campaign.CampaignId, verb)).StatusCode
                .Should().Be(HttpStatusCode.Forbidden, "staff cannot {0} a member's campaign", verb);
        }

        ActAs(organiser);

        var untouched = await GetCampaignAsync(campaign.CampaignId);

        untouched.Status.Should().Be("Published");
        untouched.Title.Should().Be("Help Ada travel home");

        (await EditAsync(campaign.CampaignId, "Help Ada get home")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await TransitionAsync(campaign.CampaignId, "pause")).StatusCode
            .Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// <b>What replaces it: a moderator suspends a live fundraiser through the moderation route.</b>
    /// The campaign leaves public view, stops collecting and tells its organiser why — and the
    /// organiser cannot resume it. Restoring it brings it back paused, for the organiser to resume.
    /// </summary>
    [Fact]
    public async Task A_moderator_suspends_a_live_campaign_and_restores_it_through_the_moderation_route()
    {
        var (_, organiser) = await SignedInMemberAsync();

        var campaign = await PublishedCampaignAsync();

        await SignInAsRoleAsync("Moderator");

        var suspended = await ModerateAsync(
            campaign.CampaignId, "Suspend", "Reported as a copy of another member's appeal.");

        suspended.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<CampaignShape>(suspended)).Status.Should().Be("Suspended");

        // A stranger can neither see it nor give to it.
        await MemberAsync();

        (await HttpClient.GetAsync(
                new Uri($"{Communities}/campaigns/{campaign.CampaignId}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await PostDonationAsync(campaign.CauseCode!, 10m)).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        // The organiser sees why, and cannot lift it or edit around it.
        ActAs(organiser);

        var seen = await HttpClient.GetAsync(
            new Uri($"{Communities}/campaigns/{campaign.CampaignId}", UriKind.Relative));

        (await ReviewNoteAsync(seen)).Should().Be("Reported as a copy of another member's appeal.");
        (await GetCampaignAsync(campaign.CampaignId)).AvailableTransitions.Should().BeEmpty();

        (await TransitionAsync(campaign.CampaignId, "resume")).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        (await EditAsync(campaign.CampaignId, "Nothing to see here")).StatusCode
            .Should().Be(HttpStatusCode.Conflict);

        await SignInAsRoleAsync("Moderator");

        var restored = await ModerateAsync(
            campaign.CampaignId, "Restore", "The organiser sent the hospital's letter.");

        restored.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadDataAsync<CampaignShape>(restored)).Status.Should().Be("Paused");

        ActAs(organiser);

        var resumed = await ReadDataAsync<CampaignShape>(
            await TransitionAsync(campaign.CampaignId, "resume"));

        resumed.Status.Should().Be("Active");
        resumed.CanDonate.Should().BeTrue();
    }

    /// <summary>The moderation route is staff's, needs a reason, and is never used on one's own.</summary>
    [Fact]
    public async Task The_moderation_route_is_staff_only_needs_a_reason_and_not_for_ones_own()
    {
        await MemberAsync();

        var campaign = await PublishedCampaignAsync();

        // The organiser cannot use it, and neither can any other member.
        (await ModerateAsync(campaign.CampaignId, "Suspend", "Hiding it myself.")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        await MemberAsync();

        (await ModerateAsync(campaign.CampaignId, "Suspend", "Because.")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);

        await SignInAsRoleAsync("Moderator");

        (await ModerateAsync(campaign.CampaignId, "Suspend", "")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        (await ModerateAsync(campaign.CampaignId, "Delete", "Because.")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);

        // A moderator's own campaign is moderated by somebody else.
        var own = await PublishedCampaignAsync();

        (await ModerateAsync(own.CampaignId, "Suspend", "Taking my own down.")).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A campaign nobody has published is visible only to the person who wrote it.
    /// <para>
    /// 404 rather than 403, so a stranger cannot confirm the identifier names something.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_draft_is_invisible_to_everybody_but_its_organizer()
    {
        await MemberAsync();

        var campaign = await CreateCampaignAsync();

        await MemberAsync();

        (await HttpClient.GetAsync(
                new Uri($"{Communities}/campaigns/{campaign.CampaignId}", UriKind.Relative)))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await SearchAsync()).Items
            .Should().NotContain(item => item.CampaignId == campaign.CampaignId);
    }

    // ── Organization campaigns ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <b>An organization campaign works end to end for somebody who manages the body.</b>
    /// <para>
    /// This test exists because of what B15.1 found the first time round: a seam that is complete,
    /// registered, architecture-tested and never actually reached. A capability seam is only proved
    /// by a request that goes all the way through it.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_organization_campaign_can_be_created_by_somebody_who_manages_the_body()
    {
        await MemberAsync();

        var organizationId = await CreateOrganizationAsync();

        var campaign = await ReadDataAsync<CampaignShape>(
            await PostCampaignAsync(organizationId: organizationId));

        campaign.OrganizationId.Should().Be(organizationId);
        campaign.CanManage.Should().BeTrue();
    }

    /// <summary>
    /// Somebody with no standing in the body is refused, server-side.
    /// <para>
    /// The register is explicit that hiding a button is not authorization.
    /// </para>
    /// </summary>
    [Fact]
    public async Task An_organization_campaign_is_refused_to_somebody_outside_the_body()
    {
        await MemberAsync();

        var organizationId = await CreateOrganizationAsync();

        await MemberAsync();

        (await PostCampaignAsync(organizationId: organizationId)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Browse ───────────────────────────────────────────────────────────────────────────────

    /// <summary>The state filter is the server's.</summary>
    [Fact]
    public async Task Campaigns_can_be_filtered_by_state()
    {
        await MemberAsync();

        var published = await PublishedCampaignAsync();

        await CreateCampaignAsync();

        var active = await SearchAsync("state=Published");

        active.Items.Should().Contain(item => item.CampaignId == published.CampaignId);
        active.Items.Should().OnlyContain(item => item.Status == "Published");
    }

    /// <summary>
    /// The page is stable across a boundary shared by several rows.
    /// <para>
    /// <b>Six campaigns created in one loop share a timestamp to the tick</b>, which is exactly the
    /// tie a strict <c>&lt;</c> cursor drops for ever. Paging two at a time must serve all six, once
    /// each.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Paging_serves_every_campaign_exactly_once()
    {
        await MemberAsync();

        var created = new List<Guid>();

        for (var index = 0; index < 6; index++)
        {
            created.Add((await CreateCampaignAsync($"Campaign {index}")).CampaignId);
        }

        var seen = new List<Guid>();
        string? cursor = null;

        for (var page = 0; page < 10; page++)
        {
            var result = await SearchAsync(
                cursor is null ? "mine=true&take=2" : $"mine=true&take=2&cursor={cursor}");

            seen.AddRange(result.Items.Select(item => item.CampaignId));

            if (!result.HasMore)
            {
                break;
            }

            cursor = result.NextCursor;
            cursor.Should().NotBeNull();
        }

        seen.Should().OnlyHaveUniqueItems("no campaign is served twice");
        seen.Should().Contain(created, "no campaign is dropped at a tie");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<PageShape<CampaignShape>> SearchAsync(string query = "") =>
        await ReadDataAsync<PageShape<CampaignShape>>(
            await HttpClient.GetAsync(
                new Uri($"{Communities}/campaigns?{query}", UriKind.Relative)));

    private async Task<Guid> CreateOrganizationAsync() =>
        (await ReadDataAsync<OrganizationShape>(
            await HttpClient.PostAsJsonAsync(
                new Uri($"{CommunityLife}/organizations", UriKind.Relative),
                new
                {
                    name = $"Association {Guid.NewGuid():N}",
                    summary = "A body created by the integration suite.",
                    organizationTypeKey = "cultural-association",
                    countryCode = "GB",
                    cityName = "London"
                }))).OrganizationId;

    private sealed record OrganizationShape(Guid OrganizationId);

    private sealed record CauseShape(string Code, string Title);
}
