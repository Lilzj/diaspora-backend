using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// A community's figures, rules, notices and office holders, a member's community preferences, and
/// the organiser's own campaigns — 21 Oct 2026, over real HTTP.
/// <para>
/// <b>Every community here is founded through the route a member uses</b>, so its owner is a real
/// Owner and Administrator with the capabilities the gates read, rather than a row a test wrote with
/// whatever roles it wanted. Only opening it is done in a scope: that is a platform administrator's
/// act, and the act is not what is under test.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityGovernanceTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/communities";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityGovernanceTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Rules
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The owner writes rules; anybody reads them, in the order the owner set.</summary>
    [Fact]
    public async Task The_owner_writes_rules_and_anybody_reads_them_in_order()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Rules order");

        var first = await ReadDataAsync<RuleDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/rules", new { title = "Be kind", body = "Treat members with respect." }));

        var second = await ReadDataAsync<RuleDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/rules",
            new { title = "No selling", body = "Use the marketplace.", consequence = "Posts removed.", position = 1 }));

        first.Position.Should().Be(1);
        second.Position.Should().Be(1, "it asked for the top");

        using var anonymous = Factory.CreateClient();
        var rules = await ReadDataAsync<List<RuleDto>>(await anonymous.GetAsync($"{Root}/{slug}/rules"));

        rules.Select(rule => rule.Title).Should().Equal("No selling", "Be kind");
        rules.Select(rule => rule.Position).Should().Equal(1, 2);
        rules[0].Consequence.Should().Be("Posts removed.");

        var revised = await ReadDataAsync<RuleDto>(await owner.PutAsJsonAsync(
            $"{Root}/{slug}/rules/{first.Id}", new { title = "Be kind always", body = "Respect.", position = 1 }));

        revised.Title.Should().Be("Be kind always");
        revised.Position.Should().Be(1);

        var remaining = await ReadDataAsync<List<RuleDto>>(
            await owner.DeleteAsync($"{Root}/{slug}/rules/{first.Id}"));

        remaining.Should().ContainSingle();
        remaining[0].Id.Should().Be(second.Id);
        remaining[0].Position.Should().Be(1, "the list is renumbered after a removal");
    }

    /// <summary>A member who does not run the community cannot write its rules.</summary>
    [Fact]
    public async Task A_member_who_does_not_run_the_community_cannot_write_rules()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Rules refused");
        var (member, _) = await MemberAsync();
        await JoinAsync(member, slug);

        var response = await member.PostAsJsonAsync(
            $"{Root}/{slug}/rules", new { title = "Mine", body = "My rule." });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).Should().Contain("rule.cannot-manage");
    }

    /// <summary>A rule without a body is refused with a sentence, not stored half-written.</summary>
    [Fact]
    public async Task A_rule_without_a_body_is_refused()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Rules invalid");

        var response = await owner.PostAsJsonAsync($"{Root}/{slug}/rules", new { title = "Empty" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("rule.body.required");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Notices
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Published by the owner, read by anybody, attributed by role.</b> The founder's membership
    /// is visible to the community only, so a stranger sees the role and not the name; a member sees
    /// both.
    /// </summary>
    [Fact]
    public async Task The_owner_publishes_notices_and_the_publishers_name_follows_their_visibility()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Notices");

        await ReadDataAsync<NoticeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/notices", new { title = "Welcome", body = "Hello everybody." }));

        var pinned = await ReadDataAsync<NoticeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/notices",
            new { title = "Meeting", body = "Saturday at noon.", isPinned = true, expiresAt = DateTimeOffset.UtcNow.AddDays(3) }));

        pinned.PublishedByRole.Should().Be("Owner");
        pinned.IsPinned.Should().BeTrue();
        pinned.CanEdit.Should().BeTrue();

        using var anonymous = Factory.CreateClient();
        var seen = await ReadDataAsync<List<NoticeDto>>(await anonymous.GetAsync($"{Root}/{slug}/notices"));

        seen.Select(notice => notice.Title).Should().Equal("Meeting", "Welcome");
        seen.Should().OnlyContain(notice => notice.PublishedByName == null && !notice.CanEdit);

        var (member, _) = await MemberAsync();
        await JoinAsync(member, slug);

        var asMember = await ReadDataAsync<List<NoticeDto>>(await member.GetAsync($"{Root}/{slug}/notices"));
        asMember.Should().OnlyContain(notice => notice.PublishedByName != null);

        var refused = await member.PostAsJsonAsync(
            $"{Root}/{slug}/notices", new { title = "Me", body = "Not allowed." });
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var revised = await ReadDataAsync<NoticeDto>(await owner.PutAsJsonAsync(
            $"{Root}/{slug}/notices/{pinned.Id}", new { title = "Meeting moved", body = "Sunday.", isPinned = false }));
        revised.Title.Should().Be("Meeting moved");
        revised.IsPinned.Should().BeFalse();
        revised.ExpiresAt.Should().BeNull("the revision sent no expiry");

        (await owner.DeleteAsync($"{Root}/{slug}/notices/{pinned.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await ReadDataAsync<List<NoticeDto>>(await anonymous.GetAsync($"{Root}/{slug}/notices"));
        after.Select(notice => notice.Title).Should().Equal("Welcome");
    }

    /// <summary>A notice cannot lapse before it is published.</summary>
    [Fact]
    public async Task A_notice_that_expires_in_the_past_is_refused()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Notice expiry");

        var response = await owner.PostAsJsonAsync(
            $"{Root}/{slug}/notices",
            new { title = "Late", body = "Too late.", expiresAt = DateTimeOffset.UtcNow.AddDays(-1) });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("notice.expiry.in-past");
    }

    /// <summary>A private community's notices are its current members' alone, and absent to anybody else.</summary>
    [Fact]
    public async Task A_private_communitys_notices_are_not_found_for_a_stranger()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Private notices", visibility: "Private");

        await ReadDataAsync<NoticeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/notices", new { title = "Inside", body = "For members." }));

        var (stranger, _) = await MemberAsync();

        (await stranger.GetAsync($"{Root}/{slug}/notices")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"{Root}/{slug}/statistics")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"{Root}/{slug}/rules")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Statistics and comparison
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Counted, not estimated.</b> Two people belong, both joined in the window, one notice was
    /// published; and the restricted figures reach the owner and not a stranger.
    /// </summary>
    [Fact]
    public async Task Statistics_count_real_rows_and_restrict_what_only_the_people_running_it_see()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Statistics");
        var (member, _) = await MemberAsync();
        await JoinAsync(member, slug);

        await ReadDataAsync<NoticeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/notices", new { title = "One", body = "A notice." }));

        using var anonymous = Factory.CreateClient();
        var open = await ReadDataAsync<StatisticsDto>(await anonymous.GetAsync($"{Root}/{slug}/statistics?period=7d"));

        open.Period.Should().Be("7d");
        open.Source.Should().Be("backend");
        Value(open, "members").Should().Be(2);
        Value(open, "newMembers").Should().Be(2, "the founder and the member both joined this week");
        Value(open, "notices").Should().Be(1);
        Value(open, "rules").Should().Be(0);
        open.Metrics.Should().OnlyContain(metric => metric.Value.Source == "backend" && !metric.IsRestricted);
        open.Metrics.Should().NotContain(metric => metric.Key == "departures", "restricted figures are omitted, not nulled");
        open.UnavailableMetrics.Should().Contain(metric => metric.Key == "jobs");
        open.Series.Should().ContainSingle().Which.Points.Should().HaveCount(7);
        open.Series[0].Points.Sum(point => point.Value).Should().Be(2);

        var restricted = await ReadDataAsync<StatisticsDto>(await owner.GetAsync($"{Root}/{slug}/statistics?period=30d"));

        Value(restricted, "departures").Should().Be(0);
        restricted.Metrics.Single(metric => metric.Key == "departures").IsRestricted.Should().BeTrue();
        restricted.UnavailableMetrics.Should().Contain(
            metric => metric.Key == "posts" && metric.Reason.Contains("fewer than"),
            "two members are too few to report activity without identifying them");
    }

    /// <summary>A period the screen does not offer is refused rather than read as thirty days.</summary>
    [Fact]
    public async Task An_unknown_period_is_refused()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Statistics period");

        var response = await owner.GetAsync($"{Root}/{slug}/statistics?period=5y");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("statistics.period.unknown");
    }

    /// <summary>Two communities side by side, aligned by metric, with no restricted row.</summary>
    [Fact]
    public async Task Comparison_aligns_two_communities_by_metric()
    {
        var (owner, _) = await MemberAsync();
        var first = await FoundAsync(owner, "Compare one");
        var second = await FoundAsync(owner, "Compare two");
        var (member, _) = await MemberAsync();
        await JoinAsync(member, second);

        var comparison = await ReadDataAsync<ComparisonDto>(
            await owner.GetAsync($"{Root}/compare?ids={first},{second}&period=30d"));

        comparison.Communities.Select(community => community.Slug).Should().Equal(first, second);
        var members = comparison.Rows.Single(row => row.Key == "members");
        members.Values.Select(value => value!.Value).Should().Equal(1, 2);
        comparison.Rows.Should().NotContain(row => row.Key == "departures");

        var tooFew = await owner.GetAsync($"{Root}/compare?ids={first}");
        tooFew.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Office holders and the ?role= filter
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A moderator visible to the community is listed to its members and to nobody else.</b> The
    /// same filter narrows the member roll.
    /// </summary>
    [Fact]
    public async Task Role_holders_are_listed_as_each_chose_to_be_seen()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Role holders");
        var (moderator, moderatorId) = await MemberAsync();
        await JoinAsync(moderator, slug);
        var (member, _) = await MemberAsync();
        await JoinAsync(member, slug);

        (await moderator.PutAsJsonAsync(
            $"{Root}/{slug}/membership/visibility", new { visibility = "CommunityOnly" }))
            .EnsureSuccessStatusCode();

        (await owner.PutAsJsonAsync(
            $"{Root}/{slug}/members/{moderatorId}/roles", new { role = "Moderator", granted = true }))
            .EnsureSuccessStatusCode();

        var moderators = await ReadDataAsync<List<RoleHolderDto>>(
            await member.GetAsync($"{Root}/{slug}/roles?role=Moderator"));

        moderators.Should().ContainSingle();
        moderators[0].UserId.Should().Be(moderatorId);
        moderators[0].Role.Should().Be("Moderator");
        moderators[0].DisplayName.Should().NotBeNullOrWhiteSpace();

        var everyOffice = await ReadDataAsync<List<RoleHolderDto>>(await member.GetAsync($"{Root}/{slug}/roles"));
        everyOffice.Select(holder => holder.Role).Should().BeEquivalentTo(["Owner", "Moderator"]);

        using var anonymous = Factory.CreateClient();
        var toStranger = await ReadDataAsync<List<RoleHolderDto>>(
            await anonymous.GetAsync($"{Root}/{slug}/roles?role=Moderator"));
        toStranger.Should().BeEmpty("the moderator chose to be visible to the community only");

        var roll = await ReadDataAsync<PageDto<MemberDto>>(
            await member.GetAsync($"{Root}/{slug}/members?role=Moderator"));
        roll.Items.Should().ContainSingle().Which.UserId.Should().Be(moderatorId);

        var unknown = await member.GetAsync($"{Root}/{slug}/roles?role=Chair");
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Preferences
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Defaults first, then what the member saved — and the saved default visibility is what the
    /// next join starts at.</b>
    /// </summary>
    [Fact]
    public async Task Preferences_default_privately_save_and_shape_the_next_join()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Preferences join");
        var (member, _) = await MemberAsync();

        var defaults = await ReadDataAsync<PreferencesDto>(await member.GetAsync($"{Root}/preferences/me"));

        defaults.IsSaved.Should().BeFalse();
        defaults.CrossCommunityDiscoveryEnabled.Should().BeTrue();
        defaults.ShowPrimaryCommunityOnProfile.Should().BeFalse();
        defaults.DefaultMembershipVisibility.Should().Be("Private");
        defaults.Notifications.Should().BeEmpty();

        var saved = await ReadDataAsync<PreferencesDto>(await member.PutAsJsonAsync(
            $"{Root}/preferences/me",
            new
            {
                crossCommunityDiscoveryEnabled = false,
                showPrimaryCommunityOnProfile = true,
                showInterestedCommunitiesOnProfile = false,
                showCommunityRolesOnProfile = true,
                preferredContentLanguage = "fr-CA",
                defaultMembershipVisibility = "CommunityOnly",
                notifications = new[]
                {
                    new { categoryKey = "events", scope = "my-communities", communityIds = new[] { Guid.NewGuid() } }
                }
            }));

        saved.IsSaved.Should().BeTrue();
        saved.CrossCommunityDiscoveryEnabled.Should().BeFalse();
        saved.PreferredContentLanguage.Should().Be("fr-CA");
        saved.Notifications.Should().ContainSingle().Which.CommunityIds.Should().BeEmpty(
            "communities only mean something under the specific scope");

        var reread = await ReadDataAsync<PreferencesDto>(await member.GetAsync($"{Root}/me/preferences"));
        reread.Should().BeEquivalentTo(saved);

        var joined = await ReadDataAsync<MembershipDto>(
            await member.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));
        joined.Visibility.Should().Be("CommunityOnly", "the member chose where new memberships start");
    }

    /// <summary>A scope the settings screen does not offer is refused.</summary>
    [Fact]
    public async Task An_unknown_notification_scope_is_refused()
    {
        var (member, _) = await MemberAsync();

        var response = await member.PutAsJsonAsync(
            $"{Root}/me/preferences",
            new { notifications = new[] { new { categoryKey = "events", scope = "everyone-everywhere" } } });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("preferences.scope.unknown");
    }

    /// <summary>Preferences are a member's own, and an anonymous caller has none to read.</summary>
    [Fact]
    public async Task Preferences_need_a_signed_in_member()
    {
        using var anonymous = Factory.CreateClient();

        (await anonymous.GetAsync($"{Root}/preferences/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Campaigns I run
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>The organiser's list answers a bare array, empty for somebody who runs none.</summary>
    [Fact]
    public async Task My_campaigns_is_a_list_and_empty_for_somebody_who_runs_none()
    {
        var (member, _) = await MemberAsync();

        var campaigns = await ReadDataAsync<List<JsonElement>>(await member.GetAsync($"{Root}/campaigns/me"));

        campaigns.Should().BeEmpty();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private static int Value(StatisticsDto statistics, string key) =>
        statistics.Metrics.Single(metric => metric.Key == key).Value.Value;

    private async Task<(HttpClient Client, Guid UserId)> MemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return (client, auth.User.Id);
    }

    /// <summary>Founds a community through the route, then opens it as staff would.</summary>
    private async Task<string> FoundAsync(HttpClient owner, string name, string visibility = "Public")
    {
        var founded = await ReadDataAsync<MembershipDto>(await owner.PostAsJsonAsync(Root, new
        {
            name = $"{name} {Guid.NewGuid():N}"[..Math.Min(name.Length + 33, 100)],
            type = "NationalDiaspora",
            visibility,
            originCountryCode = "NG"
        }));

        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = await communities.GetAsync(CommunityId.From(founded.CommunityId));
        community.Should().NotBeNull();
        community!.Open().IsSuccess.Should().BeTrue();

        await unitOfWork.SaveChangesAsync();

        return community.Slug.Value;
    }

    private static async Task JoinAsync(HttpClient member, string slug)
    {
        var joined = await ReadDataAsync<MembershipDto>(
            await member.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));

        joined.State.Should().Be("Member");
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();

        response.IsSuccessStatusCode.Should().BeTrue(
            "the request should have succeeded, and the body says why it did not: {0}", body);

        var envelope = JsonSerializer.Deserialize<EnvelopeDto<T>>(body, JsonOptions);

        envelope.Should().NotBeNull();
        envelope!.Data.Should().NotBeNull();

        return envelope.Data!;
    }

    // ── Wire shapes, re-declared by hand. See CommunityDirectoryTests. ───────────────────────

    private sealed record EnvelopeDto<T>(T? Data);

    private sealed record MembershipDto(Guid CommunityId, string State, string Visibility);

    private sealed record RuleDto(
        Guid Id, int Position, string Title, string Body, string? Consequence, DateTimeOffset? UpdatedAt);

    private sealed record NoticeDto(
        Guid Id,
        Guid CommunityId,
        string Title,
        string Body,
        DateTimeOffset PublishedAt,
        string PublishedByRole,
        string? PublishedByName,
        DateTimeOffset? ExpiresAt,
        bool IsPinned,
        bool CanEdit);

    private sealed record SourcedDto<T>(T Value, string Source);

    private sealed record MetricDto(string Key, string Label, SourcedDto<int> Value, string Period, bool IsRestricted);

    private sealed record UnavailableDto(string Key, string Label, string Reason);

    private sealed record PointDto(DateTimeOffset Start, int Value);

    private sealed record SeriesDto(string Key, string Interval, List<PointDto> Points);

    private sealed record StatisticsDto(
        Guid CommunityId,
        string Period,
        List<MetricDto> Metrics,
        List<UnavailableDto> UnavailableMetrics,
        List<SeriesDto> Series,
        string Source);

    private sealed record CommunityRefDto(Guid Id, string Slug, string Name);

    private sealed record RowDto(string Key, string Label, List<SourcedDto<int>?> Values);

    private sealed record ComparisonDto(string Period, List<CommunityRefDto> Communities, List<RowDto> Rows);

    private sealed record RoleHolderDto(Guid UserId, string DisplayName, string Role, DateTimeOffset? Since);

    private sealed record MemberDto(Guid UserId, List<string> Roles);

    private sealed record PageDto<T>(List<T> Items, bool HasMore);

    private sealed record NotificationDto(string CategoryKey, string Scope, List<Guid> CommunityIds);

    private sealed record PreferencesDto(
        bool CrossCommunityDiscoveryEnabled,
        bool ShowPrimaryCommunityOnProfile,
        bool ShowInterestedCommunitiesOnProfile,
        bool ShowCommunityRolesOnProfile,
        List<NotificationDto> Notifications,
        string? PreferredContentLanguage,
        string DefaultMembershipVisibility,
        bool IsSaved);
}
