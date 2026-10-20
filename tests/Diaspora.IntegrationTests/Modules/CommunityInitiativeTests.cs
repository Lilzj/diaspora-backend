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
/// A community's initiatives — 21 Oct 2026, over real HTTP: who may start, change, join and post to
/// one, who is named to whom, and the only ways its status moves.
/// <para>
/// Every community is founded through the member's route, as in <see cref="CommunityGovernanceTests"/>,
/// so its owner holds the roles the capability set reads. Every list asserted on belongs to a community
/// the test founded, so a shared database cannot put somebody else's row in it.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityInitiativeTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/communities";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityInitiativeTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// <b>The owner starts one; anybody reads it; a member joins and leaves.</b> The founder's
    /// membership is visible to the community only, so a stranger sees no organiser named and a
    /// member does; the counts are everybody.
    /// </summary>
    [Fact]
    public async Task The_owner_starts_an_initiative_anybody_reads_it_and_a_member_joins_and_leaves()
    {
        var (owner, ownerId) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative basics");

        var created = await ReadDataAsync<InitiativeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives",
            new { title = "Winter coats", description = "Collect coats before December.", targetDate = "2099-12-01" }));

        created.Status.Should().Be("Planned");
        created.TargetDate.Should().Be("2099-12-01");
        created.Organisers.Should().ContainSingle().Which.UserId.Should().Be(ownerId);
        created.Organisers[0].Role.Should().Be("Owner");
        created.IsOrganiser.Should().BeTrue();
        created.CanEdit.Should().BeTrue();
        created.CanPostUpdate.Should().BeTrue();
        created.ParticipantCount.Should().Be(0);

        using var anonymous = Factory.CreateClient();
        var seen = await ReadDataAsync<List<InitiativeDto>>(await anonymous.GetAsync($"{Root}/{slug}/initiatives"));

        seen.Should().ContainSingle();
        seen[0].Id.Should().Be(created.Id);
        seen[0].Organisers.Should().BeEmpty("the founder is visible to the community only");
        seen[0].CanEdit.Should().BeFalse();
        seen[0].CanJoin.Should().BeFalse();

        var (member, memberId) = await MemberAsync();
        await JoinAsync(member, slug);

        var before = await ReadDataAsync<InitiativeDto>(
            await member.GetAsync($"{Root}/{slug}/initiatives/{created.Id}"));
        before.Organisers.Should().ContainSingle().Which.UserId.Should().Be(ownerId);
        before.CanJoin.Should().BeTrue();
        before.CanEdit.Should().BeFalse();

        var joined = await ReadDataAsync<InitiativeDto>(
            await member.PostAsync($"{Root}/{slug}/initiatives/{created.Id}/participation", null));
        joined.IsParticipant.Should().BeTrue();
        joined.ParticipantCount.Should().Be(1);
        joined.CanJoin.Should().BeFalse();

        var again = await ReadDataAsync<InitiativeDto>(
            await member.PostAsync($"{Root}/{slug}/initiatives/{created.Id}/participation", null));
        again.ParticipantCount.Should().Be(1, "joining twice is joining once");

        // The member joined with a Private membership: counted for everybody, listed to nobody else.
        var toOwner = await ReadDataAsync<List<ParticipantDto>>(
            await owner.GetAsync($"{Root}/{slug}/initiatives/{created.Id}/participants"));
        toOwner.Should().BeEmpty("the member's membership is private");

        var toSelf = await ReadDataAsync<List<ParticipantDto>>(
            await member.GetAsync($"{Root}/{slug}/initiatives/{created.Id}/participants"));
        toSelf.Should().ContainSingle().Which.UserId.Should().Be(memberId);

        var left = await ReadDataAsync<InitiativeDto>(
            await member.DeleteAsync($"{Root}/{slug}/initiatives/{created.Id}/participation"));
        left.IsParticipant.Should().BeFalse();
        left.ParticipantCount.Should().Be(0);
    }

    /// <summary>
    /// <b>Participants are listed as each chose to be seen.</b> CommunityOnly to members and not to a
    /// stranger; Public to anybody. A participant who leaves the community stops being counted.
    /// </summary>
    [Fact]
    public async Task Participants_are_listed_as_each_chose_to_be_seen_and_counted_while_they_belong()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative participants");
        var initiative = await CreateAsync(owner, slug, "Clean-up day");

        var (open, openId) = await MemberAsync();
        await JoinAsync(open, slug);
        await SetVisibilityAsync(open, slug, "Public");

        var (inner, innerId) = await MemberAsync();
        await JoinAsync(inner, slug);
        await SetVisibilityAsync(inner, slug, "CommunityOnly");

        foreach (var participant in new[] { open, inner })
        {
            (await participant.PostAsync($"{Root}/{slug}/initiatives/{initiative.Id}/participation", null))
                .EnsureSuccessStatusCode();
        }

        var toMember = await ReadDataAsync<List<ParticipantDto>>(
            await owner.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}/participants"));
        toMember.Select(p => p.UserId).Should().BeEquivalentTo([openId, innerId]);
        toMember.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.DisplayName));

        using var anonymous = Factory.CreateClient();
        var toStranger = await ReadDataAsync<List<ParticipantDto>>(
            await anonymous.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}/participants"));
        toStranger.Select(p => p.UserId).Should().Equal(openId);

        var counted = await ReadDataAsync<InitiativeDto>(
            await anonymous.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}"));
        counted.ParticipantCount.Should().Be(2);

        (await inner.DeleteAsync($"{Root}/{slug}/membership"))
            .EnsureSuccessStatusCode();

        var afterLeaving = await ReadDataAsync<InitiativeDto>(
            await anonymous.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}"));
        afterLeaving.ParticipantCount.Should().Be(1, "somebody who left the community is no longer counted");
    }

    /// <summary>Joining is for members: a stranger to a public community is refused; anonymous is unauthenticated.</summary>
    [Fact]
    public async Task A_non_member_cannot_join_a_public_communitys_initiative()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative strangers");
        var initiative = await CreateAsync(owner, slug, "Book drive");

        var (stranger, _) = await MemberAsync();
        var refused = await stranger.PostAsync($"{Root}/{slug}/initiatives/{initiative.Id}/participation", null);

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("initiative.join.members-only");

        using var anonymous = Factory.CreateClient();
        (await anonymous.PostAsync($"{Root}/{slug}/initiatives/{initiative.Id}/participation", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A private community's initiatives are absent to a stranger — reading, joining and the updates alike.</summary>
    [Fact]
    public async Task A_private_communitys_initiatives_are_not_found_for_a_stranger()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative private", visibility: "Private");
        var initiative = await CreateAsync(owner, slug, "Inside job");

        var (stranger, _) = await MemberAsync();

        (await stranger.GetAsync($"{Root}/{slug}/initiatives")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"{Root}/{slug}/initiatives/{initiative.Id}/participation", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}/updates"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var anonymous = Factory.CreateClient();
        (await anonymous.GetAsync($"{Root}/{slug}/initiatives")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var asOwner = await ReadDataAsync<List<InitiativeDto>>(await owner.GetAsync($"{Root}/{slug}/initiatives"));
        asOwner.Should().ContainSingle().Which.Id.Should().Be(initiative.Id);
    }

    /// <summary>
    /// <b>A plain member can neither start, change nor post to an initiative.</b> The capability is
    /// the owner's, administrators' and ambassadors'.
    /// </summary>
    [Fact]
    public async Task A_member_who_is_not_an_organiser_cannot_start_change_or_post_to_an_initiative()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative refusals");
        var initiative = await CreateAsync(owner, slug, "Mentoring");

        var (member, _) = await MemberAsync();
        await JoinAsync(member, slug);

        var create = await member.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives", new { title = "Mine", description = "My project." });
        create.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await create.Content.ReadAsStringAsync()).Should().Contain("initiative.cannot-create");

        var change = await member.PutAsJsonAsync(
            $"{Root}/{slug}/initiatives/{initiative.Id}", new { title = "Taken over" });
        change.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await change.Content.ReadAsStringAsync()).Should().Contain("initiative.cannot-change");

        var post = await member.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives/{initiative.Id}/updates", new { title = "Hi", body = "Not mine." });
        post.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await post.Content.ReadAsStringAsync()).Should().Contain("initiative.update.cannot-post");

        using var anonymous = Factory.CreateClient();
        (await anonymous.PostAsJsonAsync($"{Root}/{slug}/initiatives", new { title = "Anon", description = "No." }))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// <b>An organiser runs the initiative but does not choose who organises it.</b> A member named
    /// organiser by the owner revises it and posts updates; changing the organisers is refused.
    /// Organisers must belong and be visible to the community.
    /// </summary>
    [Fact]
    public async Task A_named_organiser_edits_and_posts_but_cannot_change_the_organisers()
    {
        var (owner, ownerId) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative organisers");

        var (organiser, organiserId) = await MemberAsync();
        await JoinAsync(organiser, slug);

        // Joined private: nobody may be named organiser who asked not to be seen.
        var hidden = await owner.PostAsJsonAsync($"{Root}/{slug}/initiatives", new
        {
            title = "Language classes",
            description = "Weekly Yoruba classes for children.",
            organiserIds = new[] { organiserId }
        });
        hidden.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await hidden.Content.ReadAsStringAsync()).Should().Contain("initiative.organiser.not-visible");

        var (stranger, strangerId) = await MemberAsync();
        var outsider = await owner.PostAsJsonAsync($"{Root}/{slug}/initiatives", new
        {
            title = "Language classes",
            description = "Weekly Yoruba classes for children.",
            organiserIds = new[] { strangerId }
        });
        outsider.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await outsider.Content.ReadAsStringAsync()).Should().Contain("initiative.organiser.not-member");

        await SetVisibilityAsync(organiser, slug, "CommunityOnly");

        var initiative = await ReadDataAsync<InitiativeDto>(await owner.PostAsJsonAsync($"{Root}/{slug}/initiatives", new
        {
            title = "Language classes",
            description = "Weekly Yoruba classes for children.",
            organiserIds = new[] { organiserId }
        }));
        initiative.Organisers.Select(o => o.UserId).Should().Equal(organiserId);
        initiative.Organisers[0].Role.Should().Be("Member");

        var revised = await ReadDataAsync<InitiativeDto>(await organiser.PutAsJsonAsync(
            $"{Root}/{slug}/initiatives/{initiative.Id}", new { description = "Saturdays, 10am.", status = "Active" }));
        revised.Title.Should().Be("Language classes", "an omitted field is kept");
        revised.Description.Should().Be("Saturdays, 10am.");
        revised.Status.Should().Be("Active");
        revised.StartedAt.Should().NotBeNull();
        revised.IsOrganiser.Should().BeTrue();

        var update = await ReadDataAsync<UpdateDto>(await organiser.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives/{initiative.Id}/updates",
            new { title = "First class", body = "Twelve children came." }));
        update.Author.Should().NotBeNull();
        update.Author!.UserId.Should().Be(organiserId);

        var takeover = await organiser.PutAsJsonAsync(
            $"{Root}/{slug}/initiatives/{initiative.Id}", new { organiserIds = new[] { organiserId, ownerId } });
        takeover.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await takeover.Content.ReadAsStringAsync()).Should().Contain("initiative.organiser.cannot-change");

        // The owner may; and an organiser who leaves the community stops organising.
        var both = await ReadDataAsync<InitiativeDto>(await owner.PutAsJsonAsync(
            $"{Root}/{slug}/initiatives/{initiative.Id}", new { organiserIds = new[] { organiserId, ownerId } }));
        both.Organisers.Select(o => o.UserId).Should().BeEquivalentTo([organiserId, ownerId]);

        (await organiser.DeleteAsync($"{Root}/{slug}/membership"))
            .EnsureSuccessStatusCode();

        var afterLeaving = await organiser.PutAsJsonAsync(
            $"{Root}/{slug}/initiatives/{initiative.Id}", new { title = "Still mine" });
        afterLeaving.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var toMember = await ReadDataAsync<InitiativeDto>(
            await owner.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}"));
        toMember.Organisers.Select(o => o.UserId).Should().Equal(ownerId);
    }

    /// <summary>An ambassador holds canManageInitiatives, so may start one.</summary>
    [Fact]
    public async Task An_ambassador_can_start_an_initiative()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative ambassador");
        var (ambassador, ambassadorId) = await MemberAsync();
        await JoinAsync(ambassador, slug);
        await SetVisibilityAsync(ambassador, slug, "CommunityOnly");

        (await owner.PutAsJsonAsync(
            $"{Root}/{slug}/members/{ambassadorId}/roles", new { role = "Ambassador", granted = true }))
            .EnsureSuccessStatusCode();

        var created = await ReadDataAsync<InitiativeDto>(await ambassador.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives", new { title = "Welcome packs", description = "For new arrivals.", status = "Active" }));

        created.Status.Should().Be("Active");
        created.Organisers.Should().ContainSingle().Which.Role.Should().Be("Ambassador");
    }

    /// <summary>
    /// <b>Status moves forward only.</b> Planned cannot jump to Completed; a completed one is not
    /// rewritten or joined but takes a closing update; a cancelled one takes no update. Unknown and
    /// numeric statuses are refused.
    /// </summary>
    [Fact]
    public async Task Status_moves_forward_only_and_a_closed_initiative_stops_taking_part()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative status");
        var initiative = await CreateAsync(owner, slug, "Food bank");
        var path = $"{Root}/{slug}/initiatives/{initiative.Id}";

        var skip = await owner.PutAsJsonAsync(path, new { status = "Completed" });
        skip.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await skip.Content.ReadAsStringAsync()).Should().Contain("initiative.status.transition");

        (await owner.PutAsJsonAsync(path, new { status = "Finished" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PutAsJsonAsync(path, new { status = "3" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await ReadDataAsync<InitiativeDto>(await owner.PutAsJsonAsync(path, new { status = "Active" })))
            .Status.Should().Be("Active");

        var done = await ReadDataAsync<InitiativeDto>(await owner.PutAsJsonAsync(path, new { status = "Completed" }));
        done.Status.Should().Be("Completed");
        done.ClosedAt.Should().NotBeNull();
        done.CanEdit.Should().BeFalse();
        done.CanPostUpdate.Should().BeTrue("a completed initiative can still say how it ended");

        var reopen = await owner.PutAsJsonAsync(path, new { status = "Active" });
        reopen.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var rewrite = await owner.PutAsJsonAsync(path, new { title = "Rewritten" });
        rewrite.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await rewrite.Content.ReadAsStringAsync()).Should().Contain("initiative.closed");

        var (member, _) = await MemberAsync();
        await JoinAsync(member, slug);
        var join = await member.PostAsync($"{path}/participation", null);
        join.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await join.Content.ReadAsStringAsync()).Should().Contain("initiative.join.closed");

        (await owner.PostAsJsonAsync($"{path}/updates", new { title = "Thank you", body = "We fed 300 families." }))
            .EnsureSuccessStatusCode();

        var cancelled = await CreateAsync(owner, slug, "Cancelled one");
        (await owner.PutAsJsonAsync($"{Root}/{slug}/initiatives/{cancelled.Id}", new { status = "Cancelled" }))
            .EnsureSuccessStatusCode();
        var late = await owner.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives/{cancelled.Id}/updates", new { title = "Late", body = "Too late." });
        late.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await late.Content.ReadAsStringAsync()).Should().Contain("initiative.update.closed");

        var active = await ReadDataAsync<List<InitiativeDto>>(await owner.GetAsync($"{Root}/{slug}/initiatives?status=Cancelled"));
        active.Should().ContainSingle().Which.Id.Should().Be(cancelled.Id);

        (await owner.GetAsync($"{Root}/{slug}/initiatives?status=Done")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// <b>Updates read newest first, and their author is named as the author chose.</b> The owner is
    /// visible to the community only, so a stranger reads the update and not who wrote it.
    /// </summary>
    [Fact]
    public async Task Updates_read_newest_first_and_name_their_author_as_the_author_chose()
    {
        var (owner, ownerId) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative updates");
        var initiative = await CreateAsync(owner, slug, "Library corner");
        var path = $"{Root}/{slug}/initiatives/{initiative.Id}/updates";

        (await owner.PostAsJsonAsync(path, new { title = "Shelves up", body = "Two shelves." })).EnsureSuccessStatusCode();
        (await owner.PostAsJsonAsync(path, new { title = "Books in", body = "Forty books." })).EnsureSuccessStatusCode();

        var empty = await owner.PostAsJsonAsync(path, new { title = "Empty" });
        empty.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await empty.Content.ReadAsStringAsync()).Should().Contain("initiative.update.body.required");

        var (member, _) = await MemberAsync();
        await JoinAsync(member, slug);

        var toMember = await ReadDataAsync<List<UpdateDto>>(await member.GetAsync(path));
        toMember.Select(u => u.Title).Should().Equal("Books in", "Shelves up");
        toMember.Should().OnlyContain(u => u.Author != null && u.Author.UserId == ownerId);

        using var anonymous = Factory.CreateClient();
        var toStranger = await ReadDataAsync<List<UpdateDto>>(await anonymous.GetAsync(path));
        toStranger.Should().HaveCount(2);
        toStranger.Should().OnlyContain(u => u.Author == null);

        var counted = await ReadDataAsync<InitiativeDto>(
            await anonymous.GetAsync($"{Root}/{slug}/initiatives/{initiative.Id}"));
        counted.UpdateCount.Should().Be(2);
    }

    /// <summary>Validation reaches the member as a sentence, and an unknown initiative is not found.</summary>
    [Fact]
    public async Task An_invalid_initiative_is_refused_and_an_unknown_one_is_not_found()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative invalid");

        var noDescription = await owner.PostAsJsonAsync($"{Root}/{slug}/initiatives", new { title = "Empty" });
        noDescription.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await noDescription.Content.ReadAsStringAsync()).Should().Contain("initiative.description.required");

        var past = await owner.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives", new { title = "Past", description = "Too late.", targetDate = "2020-01-01" });
        past.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await past.Content.ReadAsStringAsync()).Should().Contain("initiative.target-date.in-past");

        var finished = await owner.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives", new { title = "Done", description = "Already.", status = "Completed" });
        finished.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await owner.GetAsync($"{Root}/{slug}/initiatives/{Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

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

    private static async Task<InitiativeDto> CreateAsync(HttpClient owner, string slug, string title) =>
        await ReadDataAsync<InitiativeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives", new { title, description = $"{title}, run by the community." }));

    private static async Task JoinAsync(HttpClient member, string slug)
    {
        var joined = await ReadDataAsync<MembershipDto>(
            await member.PostAsJsonAsync($"{Root}/{slug}/membership", new { action = "join" }));

        joined.State.Should().Be("Member");
    }

    private static async Task SetVisibilityAsync(HttpClient member, string slug, string visibility) =>
        (await member.PutAsJsonAsync($"{Root}/{slug}/membership/visibility", new { visibility }))
            .EnsureSuccessStatusCode();

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

    private sealed record OrganiserDto(Guid UserId, string DisplayName, string Role);

    private sealed record InitiativeDto(
        Guid Id,
        Guid CommunityId,
        string Title,
        string Description,
        string Status,
        string? TargetDate,
        DateTimeOffset? StartedAt,
        DateTimeOffset? ClosedAt,
        List<OrganiserDto> Organisers,
        int ParticipantCount,
        int UpdateCount,
        bool IsParticipant,
        bool IsOrganiser,
        bool CanJoin,
        bool CanEdit,
        bool CanPostUpdate);

    private sealed record ParticipantDto(Guid UserId, string DisplayName, DateTimeOffset JoinedAt);

    private sealed record AuthorDto(Guid UserId, string DisplayName);

    private sealed record UpdateDto(Guid Id, string Title, string Body, DateTimeOffset PostedAt, AuthorDto? Author);
}
