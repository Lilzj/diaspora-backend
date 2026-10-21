using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.Location.Domain.Areas;
using Diaspora.Modules.Location.Domain.Enums;
using Diaspora.Modules.Location.Domain.Presence;
using Diaspora.Persistence;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// The four triggers wired on 21 Oct 2026, each proven end to end over real HTTP: the producing
/// action, the integration event through the real outbox, the engine, and a run of the member's own
/// automation in its history — with the trigger's conditions shown filtering from both sides, and
/// each refusal the brief names.
/// <para>
/// Every community here is created for its test and every list read is narrowed to it (an
/// automation's runs, a community's events), so nothing asserts on a page of a list other tests also
/// write to.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class AutomationTriggerSourcesTests : PooledIdentityTestBase
{
    private const string Automations = "/api/v1/automations";
    private const string Communities = "/api/v1/communities";
    private const string Events = "/api/v1/community-life/events";
    private const string Presence = "/api/v1/location/me/presence";

    // Ealing is in London and in none of its neighbourhoods; this point is in Peckham, which is in
    // London; and this one is in Quatre Bornes Center, Mauritius. AreaFilingTests pins all three.
    private const double EalingLatitude = 51.5130d;
    private const double EalingLongitude = -0.3089d;
    private const double PeckhamLatitude = 51.4750d;
    private const double PeckhamLongitude = -0.0700d;
    private const double QuatreBornesLatitude = -20.2450d;
    private const double QuatreBornesLongitude = 57.4800d;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public AutomationTriggerSourcesTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // community.memberJoined
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A member joining runs the community's automation once, its community condition filters, and
    /// the run names nobody — the joiner kept the default Private visibility.
    /// </summary>
    [Fact]
    public async Task A_join_runs_the_communitys_automation_and_names_nobody()
    {
        var community = await SeedCommunityAsync("Join trigger", requiresApproval: false);
        var other = await SeedCommunityAsync("Join elsewhere", requiresApproval: false);

        var (admin, adminId) = await MemberClientAsync();
        await FoundAsync(community, adminId);

        var watching = await CreateAutomationAsync(
            admin, "Tell me who joins", "community.memberJoined", Community(community.Id),
            Condition("community", "equals", community.Id.ToString()));

        var mismatched = await CreateAutomationAsync(
            admin, "Only the other one", "community.memberJoined", Community(community.Id),
            Condition("community", "equals", other.Id.ToString()));

        var (joiner, joinerId) = await MemberClientAsync();
        await JoinAsync(joiner, community);
        await DrainAsync();

        var run = (await RunsAsync(admin, watching)).Should().ContainSingle().Subject;
        run.Status.Should().Be("COMPLETED");
        run.ActionOutcomes.Should().ContainSingle().Which.ActionKey.Should().Be("notify.self");

        run.TriggerSummary.Should().Be("Somebody new joined the community.");
        run.TriggerSummary.Should().NotContain(joinerId.ToString());

        var skipped = (await RunsAsync(admin, mismatched)).Should().ContainSingle().Subject;
        skipped.Status.Should().Be("SKIPPED", "the community condition names a different community");

        // A repeat join is refused and a read changes nothing: one join, one run.
        (await joiner.PostAsJsonAsync($"{Communities}/{community.Slug}/membership", new { action = "join" }))
            .IsSuccessStatusCode.Should().BeFalse();
        (await joiner.GetAsync($"{Communities}/{community.Slug}")).EnsureSuccessStatusCode();
        await DrainAsync();

        (await RunsAsync(admin, watching)).Should().ContainSingle();

        // Nothing about the run reveals the Private member to the owner.
        var raw = await (await admin.GetAsync($"{Automations}/runs?automationId={watching}"))
            .Content.ReadAsStringAsync();

        raw.Should().NotContain(joinerId.ToString(), "a Private member's belonging is theirs alone");
    }

    /// <summary>
    /// A request that waits is not a join; the approval is, and it runs the automation once.
    /// </summary>
    [Fact]
    public async Task An_approved_request_runs_the_automation_and_the_request_alone_does_not()
    {
        var community = await SeedCommunityAsync("Approval trigger", requiresApproval: true);

        var (admin, adminId) = await MemberClientAsync();
        await FoundAsync(community, adminId);

        var watching = await CreateAutomationAsync(
            admin, "Approved joins", "community.memberJoined", Community(community.Id));

        var (joiner, joinerId) = await MemberClientAsync();
        await JoinAsync(joiner, community);
        await DrainAsync();

        (await RunsAsync(admin, watching)).Should().BeEmpty("a pending request is not a member");

        await ApproveAsync(community, joinerId, adminId);
        await DrainAsync();

        (await RunsAsync(admin, watching)).Should().ContainSingle()
            .Which.Status.Should().Be("COMPLETED");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // community.eventCreated
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A member posts an event into the community: it carries the community on every read, the
    /// community's event list finds it once it is live, and the community's automation runs when it
    /// goes live — not while it is a draft.
    /// </summary>
    [Fact]
    public async Task An_event_posted_into_a_community_runs_its_automation_when_it_goes_live()
    {
        var community = await SeedCommunityAsync("Event trigger", requiresApproval: false);
        var other = await SeedCommunityAsync("Event elsewhere", requiresApproval: false);

        var (admin, adminId) = await MemberClientAsync();
        await FoundAsync(community, adminId);

        var (host, _) = await MemberClientAsync();
        await JoinAsync(host, community);
        await DrainAsync();

        var watching = await CreateAutomationAsync(
            admin, "New community events", "community.eventCreated", Community(community.Id),
            Condition("community", "equals", community.Id.ToString()));

        var mismatched = await CreateAutomationAsync(
            admin, "Other community's events", "community.eventCreated", Community(community.Id),
            Condition("community", "equals", other.Id.ToString()));

        var created = await ReadDataAsync<EventDto>(
            await host.PostAsJsonAsync(Events, EventBody("Posted in community", community.Id)));

        created.CommunityId.Should().Be(community.Id);
        created.Status.Should().Be("Draft");

        await DrainAsync();
        (await RunsAsync(admin, watching)).Should().BeEmpty("a draft is visible to its organiser alone");

        (await ReadDataAsync<EventDto>(await host.GetAsync($"{Events}/{created.EventId}")))
            .CommunityId.Should().Be(community.Id);

        (await host.PostAsJsonAsync($"{Events}/{created.EventId}/publish", new { }))
            .EnsureSuccessStatusCode();

        await DrainAsync();

        var run = (await RunsAsync(admin, watching)).Should().ContainSingle().Subject;
        run.Status.Should().Be("COMPLETED");
        run.TriggerSummary.Should().Contain("Posted in community");

        (await RunsAsync(admin, mismatched)).Should().ContainSingle()
            .Which.Status.Should().Be("SKIPPED");

        // The community's own event list: this community's events, and only those.
        var listed = await ReadDataAsync<EventPageDto>(
            await host.GetAsync($"{Events}?communityId={community.Id}"));

        listed.Items.Select(item => item.EventId).Should().Equal(created.EventId);
        listed.Items.Should().OnlyContain(item => item.CommunityId == community.Id);

        (await ReadDataAsync<EventPageDto>(await host.GetAsync($"{Events}?communityId={other.Id}")))
            .Items.Should().BeEmpty();
    }

    /// <summary>
    /// Posting into a community you do not belong to — or one that does not exist — is a 403, and
    /// no event is created.
    /// </summary>
    [Fact]
    public async Task Posting_an_event_into_a_community_you_cannot_post_in_is_forbidden()
    {
        var community = await SeedCommunityAsync("Event refused", requiresApproval: false);

        var (stranger, _) = await MemberClientAsync();

        foreach (var target in new[] { community.Id, Guid.NewGuid() })
        {
            var response = await stranger.PostAsJsonAsync(
                Events, EventBody("Not my community", target));

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await response.Content.ReadAsStringAsync())
                .Should().Contain("community-life.event.community-forbidden");
        }

        // Following is not belonging.
        (await stranger.PostAsJsonAsync($"{Communities}/{community.Slug}/membership", new { action = "follow" }))
            .EnsureSuccessStatusCode();

        (await stranger.PostAsJsonAsync(Events, EventBody("Only following", community.Id)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // And an event with no community is unaffected.
        var personal = await ReadDataAsync<EventDto>(
            await stranger.PostAsJsonAsync(Events, EventBody("No community", null)));

        personal.CommunityId.Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // location.countryChanged
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Declaring a residence where the member lives now runs their automation, with the country
    /// they moved to — and a residence already left does not.
    /// </summary>
    [Fact]
    public async Task Moving_country_runs_the_members_automation_for_the_country_entered()
    {
        var (member, _) = await MemberClientAsync();
        await DrainAsync();

        var toMauritius = await CreateAutomationAsync(
            member, "When I move to Mauritius", "location.countryChanged", owner: null,
            Condition("country", "equals", "MU"));

        var toNigeria = await CreateAutomationAsync(
            member, "When I move to Nigeria", "location.countryChanged", owner: null,
            Condition("country", "equals", "NG"));

        (await member.PostAsJsonAsync(
                "/api/v1/location/me/countries",
                new { countryCode = "MU", arrivedOn = new DateOnly(2026, 9, 1) }))
            .EnsureSuccessStatusCode();

        await DrainAsync();

        var run = (await RunsAsync(member, toMauritius)).Should().ContainSingle().Subject;
        run.Status.Should().Be("COMPLETED");
        run.TriggerSummary.Should().Contain("Mauritius");

        (await RunsAsync(member, toNigeria)).Should().ContainSingle()
            .Which.Status.Should().Be("SKIPPED", "the member moved to Mauritius, not Nigeria");

        // A residence the member has already left is history, not a move.
        (await member.PostAsJsonAsync(
                "/api/v1/location/me/countries",
                new
                {
                    countryCode = "NG",
                    arrivedOn = new DateOnly(2010, 1, 1),
                    departedOn = new DateOnly(2015, 1, 1)
                }))
            .EnsureSuccessStatusCode();

        await DrainAsync();

        (await RunsAsync(member, toMauritius)).Should().ContainSingle();
        (await RunsAsync(member, toNigeria)).Should().ContainSingle();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // location.enteredArea
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Off by default, and a ping while it is off is refused and remembered nowhere.
    /// </summary>
    [Fact]
    public async Task A_ping_while_opted_out_is_refused_and_nothing_is_stored()
    {
        var (member, memberId) = await MemberClientAsync();

        var setting = await ReadDataAsync<PresenceDto>(await member.GetAsync(Presence));
        setting.Enabled.Should().BeFalse("location while the app is open is off unless switched on");

        var refused = await PingAsync(member, PeckhamLatitude, PeckhamLongitude);

        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("location.presence.not-enabled");

        // Switching it off when it was never on writes nothing either.
        (await ReadDataAsync<PresenceDto>(
                await member.PutAsJsonAsync(Presence, new { enabled = false })))
            .Enabled.Should().BeFalse();

        (await PresenceRowAsync(memberId)).Should().BeNull();
    }

    /// <summary>
    /// <b>The whole path.</b> Opted in, a first report is a baseline; arriving in a watched
    /// neighbourhood runs the automation watching it and not the one watching the town the member
    /// was already in; arriving in a town nobody watches announces nothing. No coordinate is kept.
    /// </summary>
    [Fact]
    public async Task Entering_a_watched_area_runs_the_automation_watching_it()
    {
        var (member, memberId) = await MemberClientAsync();
        await DrainAsync();

        var peckham = await AreaIdAsync("Peckham", AdministrativeLevel.Neighborhood);
        var london = await AreaIdAsync("London", AdministrativeLevel.City);

        var capabilities = await ReadDataAsync<JsonElement>(
            await member.GetAsync($"{Automations}/capabilities"));

        capabilities.GetProperty("canUseLocationTriggers").GetBoolean().Should().BeTrue();

        var watchingPeckham = await CreateAutomationAsync(
            member, "Arriving in Peckham", "location.enteredArea", owner: null,
            Condition("neighbourhood", "equals", peckham.ToString()));

        var watchingLondon = await CreateAutomationAsync(
            member, "Arriving in London", "location.enteredArea", owner: null,
            Condition("neighbourhood", "includes", london.ToString()));

        (await ReadDataAsync<PresenceDto>(await member.PutAsJsonAsync(Presence, new { enabled = true })))
            .Enabled.Should().BeTrue();

        // In London, in no neighbourhood: the baseline, never an arrival.
        var baseline = await ReadDataAsync<PingDto>(
            await PingAsync(member, EalingLatitude, EalingLongitude, accuracy: 25));

        baseline.Outcome.Should().Be("baseline");
        baseline.Area!.AreaId.Should().Be(london);
        baseline.Announced.Should().BeFalse();

        // Straight away again: too soon, not evaluated.
        var tooSoon = await ReadDataAsync<PingDto>(
            await PingAsync(member, PeckhamLatitude, PeckhamLongitude));

        tooSoon.Evaluated.Should().BeFalse();
        tooSoon.Outcome.Should().Be("tooSoon");

        await RewindPresenceAsync(memberId);

        // A fix vaguer than a kilometre is not evaluated either.
        (await ReadDataAsync<PingDto>(
                await PingAsync(member, PeckhamLatitude, PeckhamLongitude, accuracy: 5_000)))
            .Outcome.Should().Be("tooCoarse");

        var arrived = await ReadDataAsync<PingDto>(
            await PingAsync(member, PeckhamLatitude, PeckhamLongitude, accuracy: 30));

        arrived.Outcome.Should().Be("entered");
        arrived.Area!.AreaId.Should().Be(peckham);
        arrived.Announced.Should().BeTrue();

        await DrainAsync();

        var run = (await RunsAsync(member, watchingPeckham)).Should().ContainSingle().Subject;
        run.Status.Should().Be("COMPLETED");
        run.TriggerSummary.Should().Be("You arrived in Peckham.");

        // London's rule saw the same arrival and said no: the member was already in London, and
        // walking into one of its neighbourhoods is not arriving there.
        (await RunsAsync(member, watchingLondon)).Should().ContainSingle()
            .Which.Status.Should().Be("SKIPPED");

        // Mauritius: an arrival, but in nothing either rule watches, so nothing is announced.
        await RewindPresenceAsync(memberId);

        var elsewhere = await ReadDataAsync<PingDto>(
            await PingAsync(member, QuatreBornesLatitude, QuatreBornesLongitude));

        elsewhere.Outcome.Should().Be("entered");
        elsewhere.Announced.Should().BeFalse("no rule of this member's watches Mauritius");

        await DrainAsync();
        (await RunsAsync(member, watchingPeckham)).Should().ContainSingle();
        (await RunsAsync(member, watchingLondon)).Should().ContainSingle("nothing was published");

        // Back to London: London is new again, so the town's rule fires now.
        await RewindPresenceAsync(memberId);

        (await ReadDataAsync<PingDto>(await PingAsync(member, PeckhamLatitude, PeckhamLongitude)))
            .Announced.Should().BeTrue();

        await DrainAsync();
        (await RunsAsync(member, watchingLondon)).Select(run => run.Status)
            .Should().BeEquivalentTo(["SKIPPED", "COMPLETED"]);
        (await RunsAsync(member, watchingPeckham)).Select(run => run.Status)
            .Should().BeEquivalentTo(["COMPLETED", "COMPLETED"]);

        // Kept: an area id and a time. Switching off forgets the area.
        (await PresenceRowAsync(memberId))!.LastAreaId.Should().Be(peckham);

        (await ReadDataAsync<PresenceDto>(await member.PutAsJsonAsync(Presence, new { enabled = false })))
            .LastArea.Should().BeNull();

        (await PresenceRowAsync(memberId))!.LastAreaId.Should().BeNull();

        (await PingAsync(member, PeckhamLatitude, PeckhamLongitude))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>The presence setting is in the member's export and erasure deletes it.</summary>
    [Fact]
    public async Task Presence_is_exported_and_erased_with_the_account()
    {
        var (member, memberId) = await MemberClientAsync();

        await member.PutAsJsonAsync(Presence, new { enabled = true });
        (await PingAsync(member, PeckhamLatitude, PeckhamLongitude)).EnsureSuccessStatusCode();

        using (var scope = Factory.Services.CreateScope())
        {
            var exporter = scope.ServiceProvider.GetServices<IDataExportProvider>()
                .Single(provider => provider.Module == "Location");

            var sections = await exporter.ExportAsync(memberId);

            var presence = sections.Should()
                .ContainSingle(section => section.DataSet == "location while the app is open").Subject;

            presence.Records.Should().ContainSingle();
            presence.Records[0]["enabled"].Should().Be(true);
            presence.Records[0].Keys.Should().NotContain(["latitude", "longitude"]);
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var eraser = scope.ServiceProvider.GetServices<IDataErasureProvider>()
                .Single(provider => provider.Module == "Location");

            var results = await eraser.EraseAsync(memberId);

            results.Should().Contain(result =>
                result.DataSet == "location while the app is open"
                && result.Outcome == ErasureOutcome.Deleted);
        }

        (await PresenceRowAsync(memberId)).Should().BeNull();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // event.* — who may hear about somebody else's event (21 Oct 2026)
    //
    // `event.created` used to fire on the draft for every member's automation, so a private draft's
    // title reached strangers. Every run list below is narrowed to this test's own event by its
    // unique title: these rules watch for anybody's events, and the collection's other tests
    // publish public ones into the same database.
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A draft runs nobody's <c>event.created</c> rule, the host's included; publishing it as a
    /// public event runs both the stranger's rule and the host's, once.
    /// </summary>
    [Fact]
    public async Task A_draft_runs_nothing_and_a_public_publication_runs_everybodys_rule()
    {
        var (host, _) = await MemberClientAsync();
        var (stranger, _) = await MemberClientAsync();

        var hostRule = await CreateAutomationAsync(host, "My new events", "event.created", null);
        var strangerRule = await CreateAutomationAsync(stranger, "Any new event", "event.created", null);

        var created = await ReadDataAsync<EventDetailDto>(
            await host.PostAsJsonAsync(Events, EventBody("Draft leak probe", null)));

        created.Status.Should().Be("Draft");

        await DrainAsync();

        (await RunsAbout(stranger, strangerRule, created.Title))
            .Should().BeEmpty("a draft is visible to its organiser alone");
        (await RunsAbout(host, hostRule, created.Title))
            .Should().BeEmpty("a new event is posted when it is published, not drafted");

        (await host.PostAsJsonAsync($"{Events}/{created.EventId}/publish", new { }))
            .EnsureSuccessStatusCode();

        await DrainAsync();

        (await RunsAbout(stranger, strangerRule, created.Title))
            .Should().ContainSingle().Which.Status.Should().Be("COMPLETED");
        (await RunsAbout(host, hostRule, created.Title))
            .Should().ContainSingle().Which.Status.Should().Be("COMPLETED");
    }

    /// <summary>
    /// An event published unlisted — reachable by its link, absent from search — runs its host's
    /// own rule and nobody else's.
    /// </summary>
    [Fact]
    public async Task An_unlisted_event_reaches_its_host_and_nobody_else()
    {
        var (host, _) = await MemberClientAsync();
        var (stranger, _) = await MemberClientAsync();

        var hostRule = await CreateAutomationAsync(host, "My new events", "event.created", null);
        var strangerRule = await CreateAutomationAsync(stranger, "Any new event", "event.created", null);

        var created = await ReadDataAsync<EventDetailDto>(
            await host.PostAsJsonAsync(Events, EventBody("Unlisted leak probe", null)));

        var published = await ReadDataAsync<EventDetailDto>(
            await host.PostAsJsonAsync(
                $"{Events}/{created.EventId}/publish", new { visibility = "Unlisted" }));

        published.Visibility.Should().Be("Unlisted");

        await DrainAsync();

        (await RunsAbout(stranger, strangerRule, created.Title))
            .Should().BeEmpty("a stranger could not have found an unlisted event");
        (await RunsAbout(host, hostRule, created.Title))
            .Should().ContainSingle().Which.Status.Should().Be("COMPLETED");
    }

    /// <summary>
    /// A public event made private stops reaching strangers: its later edit runs the host's
    /// <c>event.updated</c> rule only. And a cancelled draft announces itself to nobody but its host.
    /// </summary>
    [Fact]
    public async Task Updates_and_cancellations_of_a_private_event_reach_its_host_alone()
    {
        var (host, _) = await MemberClientAsync();
        var (stranger, _) = await MemberClientAsync();

        var hostUpdated = await CreateAutomationAsync(host, "Mine changed", "event.updated", null);
        var strangerUpdated = await CreateAutomationAsync(stranger, "Any changed", "event.updated", null);
        var hostCancelled = await CreateAutomationAsync(host, "Mine off", "event.cancelled", null);
        var strangerCancelled = await CreateAutomationAsync(stranger, "Any off", "event.cancelled", null);

        // Public, then made private: the edit that does it, and every edit after, is the host's.
        var live = await ReadDataAsync<EventDetailDto>(
            await host.PostAsJsonAsync(Events, EventBody("Made private probe", null)));

        (await host.PostAsJsonAsync($"{Events}/{live.EventId}/publish", new { }))
            .EnsureSuccessStatusCode();
        await DrainAsync();

        (await ReadDataAsync<EventDetailDto>(
                await host.PatchAsJsonAsync($"{Events}/{live.EventId}", new { visibility = "Private" })))
            .Visibility.Should().Be("Private");

        await DrainAsync();

        (await RunsAbout(stranger, strangerUpdated, live.Title))
            .Should().BeEmpty("the event is no longer anybody's but its host's");
        (await RunsAbout(host, hostUpdated, live.Title)).Should().ContainSingle();

        // A draft called off: its title was only ever its organiser's.
        var draft = await ReadDataAsync<EventDetailDto>(
            await host.PostAsJsonAsync(Events, EventBody("Cancelled draft probe", null)));

        (await host.PostAsJsonAsync($"{Events}/{draft.EventId}/cancel", new { reason = "Probe" }))
            .EnsureSuccessStatusCode();

        await DrainAsync();

        (await RunsAbout(stranger, strangerCancelled, draft.Title))
            .Should().BeEmpty("a cancelled draft was never anybody else's to hear about");
        (await RunsAbout(host, hostCancelled, draft.Title)).Should().ContainSingle();
    }

    private static async Task<IReadOnlyList<RunDto>> RunsAbout(
        HttpClient client, string automationId, string title) =>
        [.. (await RunsAsync(client, automationId))
            .Where(run => run.TriggerSummary.Contains(title, StringComparison.Ordinal))];

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid UserId)> MemberClientAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return (client, auth.User.Id);
    }

    private static object Community(Guid id) => new { ownerKind = "community", ownerId = id };

    private static object Condition(string key, string @operator, string value) =>
        new { key, @operator, value };

    private static async Task<string> CreateAutomationAsync(
        HttpClient client, string name, string trigger, object? owner, params object[] conditions)
    {
        var response = await client.PostAsJsonAsync(
            Automations,
            new
            {
                name,
                scope = owner,
                trigger = new { key = trigger },
                conditions,
                actions = new[] { new { key = "notify.self" } },
                status = "ACTIVE",
                idempotencyKey = Guid.NewGuid().ToString("N")
            },
            JsonOptions);

        var created = await ReadDataAsync<AutomationDto>(response);
        created.Status.Should().Be("ACTIVE");
        return created.Id;
    }

    private static async Task<IReadOnlyList<RunDto>> RunsAsync(HttpClient client, string automationId) =>
        (await ReadDataAsync<PageDto<RunDto>>(
            await client.GetAsync($"{Automations}/runs?automationId={automationId}"))).Items;

    private static async Task JoinAsync(HttpClient client, SeededCommunity community) =>
        (await client.PostAsJsonAsync($"{Communities}/{community.Slug}/membership", new { action = "join" }))
            .EnsureSuccessStatusCode();

    private async Task<SeededCommunity> SeedCommunityAsync(string name, bool requiresApproval)
    {
        var slug = CommunitySlug.FromName($"{name}-{Guid.NewGuid():N}").Value;

        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = CommunityAggregate.Propose(
            CommunityId.New(), slug, name,
            CommunityType.NationalDiaspora, CommunityVisibility.Public, "NG").Value;

        community.Open();
        community.SetJoinPolicy(requiresApproval);

        communities.Add(community);
        await unitOfWork.SaveChangesAsync();

        return new SeededCommunity(community.Id.Value, slug.Value);
    }

    /// <summary>
    /// The community's Owner and Administrator, as founding makes them. Founding is not a join, so
    /// it publishes nothing that a join rule could see.
    /// </summary>
    private async Task FoundAsync(SeededCommunity community, Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var memberships = scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var aggregate = await communities.GetAsync(CommunityId.From(community.Id));

        memberships.Add(CommunityMembership.Found(
            CommunityMembershipId.New(), aggregate!, UserId.From(userId), DateTimeOffset.UtcNow).Value);

        await unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Delivers everything queued, however long the collection's backlog. One call of the driver
    /// stops after twenty passes, and this collection's shared database can hold more than that
    /// from the suites before it — a fact published here would then arrive on a later drain, and a
    /// test would see another test's timing rather than its own.
    /// </summary>
    private async Task DrainAsync()
    {
        for (var round = 0; round < 30; round++)
        {
            await Factory.DrainOutboxAsync(maxPasses: 100);

            if ((await Factory.PendingOutboxAsync(limit: 1)).Count == 0)
            {
                return;
            }

            await Task.Delay(250);
        }
    }

    /// <summary>
    /// Approves a pending request through the aggregate. No route approves a community join request
    /// yet; the domain transition is what publishes the join, and it is what this exercises.
    /// </summary>
    private async Task ApproveAsync(SeededCommunity community, Guid userId, Guid approver)
    {
        using var scope = Factory.Services.CreateScope();
        var memberships = scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var membership = await memberships.FindAsync(CommunityId.From(community.Id), UserId.From(userId));

        membership!.State.Should().Be(CommunityMembershipState.PendingApproval);
        membership.Approve(UserId.From(approver), DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();

        await unitOfWork.SaveChangesAsync();
    }

    private static object EventBody(string title, Guid? communityId) =>
        new
        {
            title = $"{title} {Guid.NewGuid():N}",
            summary = "An event created by the automation trigger suite.",
            categoryKey = "community-meeting",
            communityId,
            startsAt = DateTimeOffset.UtcNow.AddDays(14),
            endsAt = DateTimeOffset.UtcNow.AddDays(14).AddHours(3),
            countryCode = "GB",
            cityName = "London",
            venue = new { name = "Community Hall", addressLine1 = "1 High Street" }
        };

    private static Task<HttpResponseMessage> PingAsync(
        HttpClient client, double latitude, double longitude, double? accuracy = null) =>
        client.PostAsJsonAsync(
            $"{Presence}/ping",
            new { latitude, longitude, accuracyMetres = accuracy });

    /// <summary>Moves the last evaluation back past the interval, as time passing would.</summary>
    private async Task RewindPresenceAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();

        var presence = await db.Set<MemberPresence>()
            .SingleAsync(row => row.UserId == UserId.From(userId));

        db.Entry(presence).Property(row => row.LastEvaluatedAt).CurrentValue =
            presence.LastEvaluatedAt!.Value.AddMinutes(-5);

        await db.SaveChangesAsync();
    }

    private async Task<MemberPresence?> PresenceRowAsync(Guid userId)
    {
        using var scope = Factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<MemberPresence>().AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == UserId.From(userId));
    }

    private async Task<Guid> AreaIdAsync(string name, AdministrativeLevel level)
    {
        using var scope = Factory.Services.CreateScope();

        return (await scope.ServiceProvider.GetRequiredService<DiasporaDbContext>()
            .Set<AdministrativeArea>().AsNoTracking()
            .SingleAsync(area => area.Level == level && area.Name.Value == name)).Id.Value;
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("data").Deserialize<T>(JsonOptions)!;
    }

    private sealed record SeededCommunity(Guid Id, string Slug);

    private sealed record AutomationDto(string Id, string Status);

    private sealed record PageDto<T>(IReadOnlyList<T> Items);

    private sealed record RunDto(
        string Id,
        string Status,
        string TriggerSummary,
        IReadOnlyList<ActionOutcomeDto> ActionOutcomes);

    private sealed record ActionOutcomeDto(string ActionKey, string Status);

    private sealed record EventDto(Guid EventId, string Status, Guid? CommunityId);

    private sealed record EventDetailDto(Guid EventId, string Status, string Title, string Visibility);

    private sealed record EventSummaryDto(Guid EventId, Guid? CommunityId);

    private sealed record EventPageDto(IReadOnlyList<EventSummaryDto> Items);

    private sealed record AreaDto(Guid AreaId, string Name, string Level);

    private sealed record PresenceDto(bool Enabled, AreaDto? LastArea);

    private sealed record PingDto(bool Evaluated, string Outcome, AreaDto? Area, bool Announced);
}
