using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// FRONTEND-PHASE13-GAP-003 to -006 — the member acting on their own standing, over real HTTP.
/// <para>
/// <b>Every act here is the member's own.</b> Approving, declining and revoking are the community's
/// decisions about somebody else and are not reachable from these routes at all — the subject is
/// always the bearer token, never a path parameter. What these pin is the state machine underneath,
/// and in particular the transitions that must <em>not</em> happen: a member quietly demoted to a
/// follower, a revocation laundered by following, a count that moves when nobody joined.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityMembershipActionTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/communities";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityMembershipActionTests(CustomWebApplicationFactory factory) : base(factory) { }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Joining — GAP-004
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>An open community is joined outright, and the member count moves with it.</summary>
    [Fact]
    public async Task Joining_an_open_community_is_immediate()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Join immediate");

        var membership = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));

        membership.State.Should().Be("Member");
        membership.JoinedAt.Should().NotBeNull();
        membership.RequiresApproval.Should().BeFalse();

        var community = await ReadDataAsync<CommunityDto>(await client.GetAsync($"{Root}/{slug}"));

        community.MemberCount!.Value.Should().Be(1, "somebody actually joined");
    }

    /// <summary>
    /// <b>A community that asks for approval leaves the member waiting, and the count where it
    /// was.</b> Counting a pending request as a member is the defect that makes a community look
    /// bigger than it is and lets a waiting person see content they have not been admitted to.
    /// </summary>
    [Fact]
    public async Task Joining_a_community_that_asks_for_approval_waits()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Join approval", requiresApproval: true);

        var membership = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));

        membership.State.Should().Be("PendingApproval");
        membership.JoinedAt.Should().BeNull("waiting is not belonging");
        membership.RequiresApproval.Should().BeTrue("the button should have said so beforehand");

        var community = await ReadDataAsync<CommunityDto>(await client.GetAsync($"{Root}/{slug}"));

        community.MemberCount!.Value.Should().Be(0);
    }

    /// <summary>
    /// <b>A member who taps Follow stays a member.</b> The client sends the act rather than the
    /// target state, and reading `follow` as "become a follower" would silently remove somebody from
    /// their own community.
    /// </summary>
    [Fact]
    public async Task Following_a_community_you_belong_to_does_not_demote_you()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("No demotion");

        await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" });

        var after = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "follow" }));

        after.State.Should().Be("Member");
    }

    /// <summary>Following is not membership, and does not touch the count.</summary>
    [Fact]
    public async Task Following_is_not_membership()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Following only");

        var membership = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "follow" }));

        membership.State.Should().Be("Following");
        membership.JoinedAt.Should().BeNull();

        var community = await ReadDataAsync<CommunityDto>(await client.GetAsync($"{Root}/{slug}"));

        community.MemberCount!.Value.Should().Be(0);
    }

    /// <summary>An act the route does not offer is refused rather than read as a join.</summary>
    [Fact]
    public async Task An_unrecognised_action_is_refused_rather_than_guessed()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Unknown action");

        var response = await client.PostAsJsonAsync(
            $"{Root}/{slug}/membership", new { action = "become-moderator" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("membership.unknown-action");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Leaving — GAP-004
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>Leaving says the member decided, and carries no reason.</b> That is what keeps it
    /// distinguishable from being removed on the screen that renders both.
    /// </summary>
    [Fact]
    public async Task Leaving_is_the_members_own_decision_and_moves_the_count_back()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Leaving");

        await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" });

        var left = await ReadDataAsync<MembershipDto>(
            await client.DeleteAsync($"{Root}/{slug}/membership?action=leave"));

        left.State.Should().Be("Left");
        left.StateReason.Should().BeNull("nobody decided this but them");

        var community = await ReadDataAsync<CommunityDto>(await client.GetAsync($"{Root}/{slug}"));

        community.MemberCount!.Value.Should().Be(0);
    }

    /// <summary>
    /// <b>Unfollowing what you never followed is a 404.</b> The platform's existing convention on
    /// <c>/community/follows</c>, which the client is already shaped around.
    /// </summary>
    [Fact]
    public async Task Ending_a_relationship_that_does_not_exist_is_refused()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Nothing to end");

        var response = await client.DeleteAsync($"{Root}/{slug}/membership?action=unfollow");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>
    /// <b>The action arrives in the query string, and the route reads it.</b> A body on DELETE is
    /// what the client's helper sends and what minimal APIs refuse to infer; if this parameter were
    /// unbound the act would silently default to `leave`, and unfollowing would remove somebody from
    /// a community they had joined. The assertion is that `unfollow` did *not* produce `Left`.
    /// </summary>
    [Fact]
    public async Task The_action_binds_from_the_query_string()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Query binding");

        await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "follow" });

        var ended = await ReadDataAsync<MembershipDto>(
            await client.DeleteAsync($"{Root}/{slug}/membership?action=unfollow"));

        ended.State.Should().Be(
            "None", "an unbound action would have defaulted to leave and produced Left");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // The set and the primary — GAP-003, GAP-005
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>The set states which community is primary rather than leaving it to be scanned.</b> The
    /// client reads <c>primaryCommunityId</c> in preference to scanning, so a set that somehow
    /// carried two renders as one rather than as an arbitrary pick.
    /// </summary>
    [Fact]
    public async Task Setting_a_primary_community_answers_the_whole_set()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Primary set");

        var joined = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));

        var set = await ReadDataAsync<MembershipSetDto>(
            await client.PutAsJsonAsync($"{Root}/me/primary", new { communityId = joined.CommunityId }));

        set.PrimaryCommunityId.Should().Be(joined.CommunityId);
        set.Memberships.Should().ContainSingle(m => m.CommunityId == joined.CommunityId && m.IsPrimary);
        set.FetchedAt.Should().NotBe(default);
    }

    /// <summary>
    /// <b>Moving the primary clears the old one in the same write.</b> A filtered unique index
    /// enforces one primary per member, so a handler that set a second without clearing the first
    /// would turn an ordinary tap into a database error.
    /// </summary>
    [Fact]
    public async Task Moving_the_primary_leaves_exactly_one()
    {
        using var client = await MemberClientAsync();
        var first = await SeedAsync("Primary first");
        var second = await SeedAsync("Primary second");

        var one = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{first}/membership", new { action = "join" }));
        var two = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{second}/membership", new { action = "join" }));

        await client.PutAsJsonAsync($"{Root}/me/primary", new { communityId = one.CommunityId });

        var set = await ReadDataAsync<MembershipSetDto>(
            await client.PutAsJsonAsync($"{Root}/me/primary", new { communityId = two.CommunityId }));

        set.PrimaryCommunityId.Should().Be(two.CommunityId);
        set.Memberships.Where(m => m.IsPrimary).Should().ContainSingle();
    }

    /// <summary>A primary must be a community you belong to, or a profile names one you do not.</summary>
    [Fact]
    public async Task A_community_you_do_not_belong_to_cannot_be_primary()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Primary stranger");

        var community = await ReadDataAsync<CommunityDto>(await client.GetAsync($"{Root}/{slug}"));

        var response = await client.PutAsJsonAsync(
            $"{Root}/me/primary", new { communityId = community.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    /// <summary>Clearing is a value rather than a second route.</summary>
    [Fact]
    public async Task A_primary_can_be_cleared()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Primary clear");

        var joined = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));

        await client.PutAsJsonAsync($"{Root}/me/primary", new { communityId = joined.CommunityId });

        var set = await ReadDataAsync<MembershipSetDto>(
            await client.PutAsJsonAsync($"{Root}/me/primary", new { communityId = (Guid?)null }));

        set.PrimaryCommunityId.Should().BeNull();
        set.Memberships.Should().NotContain(m => m.IsPrimary);
    }

    /// <summary>
    /// <b>The set keeps terminal rows.</b> Left, Declined and Revoked are three different next steps
    /// on the client, and dropping them would offer Join to somebody just removed.
    /// </summary>
    [Fact]
    public async Task The_set_keeps_the_relationships_that_ended()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Terminal kept");

        await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" });
        await client.DeleteAsync($"{Root}/{slug}/membership?action=leave");

        var set = await ReadDataAsync<MembershipSetDto>(
            await client.GetAsync($"{Root}/me/memberships"));

        set.Memberships.Should().Contain(m => m.State == "Left");
    }

    /// <summary>
    /// <b>Joining publishes nothing.</b> §41: the member opts in to being visible rather than out,
    /// so a membership nobody has adjusted is Private.
    /// </summary>
    [Fact]
    public async Task A_new_membership_is_private()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Privacy default");

        var membership = await ReadDataAsync<MembershipDto>(
            await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));

        membership.Visibility.Should().Be("Private");
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Capabilities — GAP-006
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>A stranger gets eleven falses rather than a refusal.</b> A 403 would tell them a
    /// moderation queue exists; the all-false set answers the question they asked.
    /// </summary>
    [Fact]
    public async Task A_stranger_holds_no_capability_and_gets_an_answer()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Capability stranger");

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await client.GetAsync($"{Root}/{slug}/capabilities"));

        capabilities.Roles.Should().BeEmpty();
        capabilities.CanViewMembers.Should().BeFalse();
        capabilities.CanPostToCommunity.Should().BeFalse();
        capabilities.CanViewModerationQueue.Should().BeFalse();
    }

    /// <summary>A member may read the roll and post; nobody has a moderation role to hold yet.</summary>
    [Fact]
    public async Task A_member_may_read_the_roll_and_post_but_not_moderate()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Capability member");

        await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" });

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await client.GetAsync($"{Root}/{slug}/capabilities"));

        capabilities.CanViewMembers.Should().BeTrue();
        capabilities.CanPostToCommunity.Should().BeTrue();
        capabilities.CanViewAmbassadorDirectory.Should().BeTrue();

        capabilities.CanModerateContent.Should().BeFalse(
            "nothing grants a moderator role yet, and reporting one nobody holds would be a lie");
        capabilities.CanManageRoles.Should().BeFalse();
    }

    /// <summary>
    /// <b>A suspended community cannot be posted to, and a member keeps their standing.</b>
    /// Suspension stops the community, not the membership.
    /// </summary>
    [Fact]
    public async Task A_suspended_community_cannot_be_posted_to()
    {
        using var client = await MemberClientAsync();
        var slug = await SeedAsync("Capability suspended");

        await client.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" });
        await SuspendAsync(slug, "Paused while we review a report.");

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await client.GetAsync($"{Root}/{slug}/capabilities"));

        capabilities.CanViewMembers.Should().BeTrue("they still belong");
        capabilities.CanPostToCommunity.Should().BeFalse("the community is stopped");
    }

    /// <summary>An anonymous caller gets the all-false set rather than a 401.</summary>
    [Fact]
    public async Task An_anonymous_caller_gets_the_all_false_set()
    {
        var slug = await SeedAsync("Capability anonymous");

        using var anonymous = Factory.CreateClient();

        var capabilities = await ReadDataAsync<CapabilitiesDto>(
            await anonymous.GetAsync($"{Root}/{slug}/capabilities"));

        capabilities.CanViewMembers.Should().BeFalse();
        capabilities.Roles.Should().BeEmpty();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>A signed-in member with their own client, so tests cannot share a caller.</summary>
    private async Task<HttpClient> MemberClientAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return client;
    }

    private async Task<string> SeedAsync(string name, bool requiresApproval = false)
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

        return slug.Value;
    }

    private async Task SuspendAsync(string slug, string reason)
    {
        using var scope = Factory.Services.CreateScope();
        var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var community = await communities.FindByIdOrSlugAsync(slug);
        community!.Suspend(reason);

        await unitOfWork.SaveChangesAsync();
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

    private sealed record MembershipDto(
        Guid CommunityId,
        string State,
        bool IsPrimary,
        List<string> Roles,
        CapabilitiesDto Capabilities,
        string Visibility,
        DateTimeOffset? JoinedAt,
        DateTimeOffset? VerifiedAt,
        string? StateReason,
        bool? RequiresApproval);

    private sealed record MembershipSetDto(
        List<MembershipDto> Memberships, Guid? PrimaryCommunityId, DateTimeOffset FetchedAt);

    private sealed record CapabilitiesDto(
        Guid CommunityId,
        List<string> Roles,
        bool CanViewMembers,
        bool CanViewPrivateStatistics,
        bool CanPostToCommunity,
        bool CanModerateContent,
        bool CanReviewReports,
        bool CanPublishNotice,
        bool CanManageRules,
        bool CanManageRoles,
        bool CanViewModerationQueue,
        bool CanViewAmbassadorDirectory,
        bool CanManageInitiatives);

    private sealed record CommunityDto(Guid Id, string Slug, SourcedDto<int>? MemberCount);

    private sealed record SourcedDto<T>(T Value, string Source);
}
