using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Diaspora.Domain.Abstractions;
using Diaspora.IntegrationTests.Common;
using Diaspora.Modules.CommunityLife.Integration;
using Diaspora.Modules.CommunityProgramme.Application.Communities;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diaspora.IntegrationTests.Modules;

/// <summary>
/// Account erasure and data export reach community governance and initiatives — 21 Oct 2026.
/// <para>
/// The rules, notices, preferences and initiatives shipped with no privacy provider, so a closed
/// account kept its preferences and participations, and stayed named on every notice, update and
/// initiative it touched. And two simultaneous joins of one initiative answered 500.
/// </para>
/// </summary>
[Collection(CommunityCollection.Name)]
public sealed class CommunityProgrammePrivacyTests : PooledIdentityTestBase
{
    private const string Root = "/api/v1/communities";
    private const string Module = "CommunityProgramme";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public CommunityProgrammePrivacyTests(CustomWebApplicationFactory factory) : base(factory) { }

    /// <summary>
    /// <b>Erasure: what is theirs goes, what is the community's stays without them.</b> Preferences
    /// and participations are deleted; the notice and the update keep their text and lose their
    /// author; the member leaves both organiser lists, and the initiative they organised alone is
    /// still readable and still manageable by the owner.
    /// </summary>
    [Fact]
    public async Task Erasure_deletes_what_is_the_members_and_anonymises_what_is_the_communitys()
    {
        var world = await SeedAsync();

        using (var scope = Factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider.GetServices<IDataErasureProvider>()
                .Should().ContainSingle(candidate => candidate.Module == Module).Subject;

            var preview = await provider.PreviewAsync(world.MemberId);

            Outcome(preview, "community preferences").Should().Be((ErasureOutcome.Deleted, 1));
            Outcome(preview, "initiative participations").Should().Be((ErasureOutcome.Deleted, 1));
            Outcome(preview, "community notices published").Should().Be((ErasureOutcome.Anonymized, 1));
            Outcome(preview, "initiative updates posted").Should().Be((ErasureOutcome.Anonymized, 1));
            Outcome(preview, "initiatives proposed or organised").Should().Be((ErasureOutcome.Anonymized, 2));
            preview.Should().Contain(result => result.DataSet == "community memberships"
                && result.Outcome == ErasureOutcome.Retained
                && !string.IsNullOrWhiteSpace(result.Reason),
                "a data set this provider does not erase is named in the receipt, not left out of it");

            var erased = await provider.EraseAsync(world.MemberId);

            Outcome(erased, "community preferences").Should().Be((ErasureOutcome.Deleted, 1));
            Outcome(erased, "initiative participations").Should().Be((ErasureOutcome.Deleted, 1));
            Outcome(erased, "community notices published").Should().Be((ErasureOutcome.Anonymized, 1));
            Outcome(erased, "initiative updates posted").Should().Be((ErasureOutcome.Anonymized, 1));
            Outcome(erased, "initiatives proposed or organised").Should().Be((ErasureOutcome.Anonymized, 2));
        }

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
            var member = UserId.From(world.MemberId);
            var stamp = world.MemberId.ToString();

            (await db.Set<CommunityMemberPreferences>().AnyAsync(row => row.MemberId == member))
                .Should().BeFalse();
            (await db.Set<CommunityInitiativeParticipant>().AnyAsync(row => row.MemberId == member))
                .Should().BeFalse();

            var notice = await db.Set<CommunityNotice>().AsNoTracking()
                .SingleAsync(row => row.Id == CommunityNoticeId.From(world.NoticeId));
            notice.PublishedBy.Should().Be(UserId.Empty);
            notice.Title.Should().Be("Hall closed on Friday", "the community's notice is kept");
            notice.PublishedByRole.Should().Be("Moderator", "a role names nobody");

            var update = await db.Set<CommunityInitiativeUpdate>().AsNoTracking()
                .SingleAsync(row => row.InitiativeId == CommunityInitiativeId.From(world.SharedInitiativeId));
            update.AuthorId.Should().Be(UserId.Empty);
            update.Body.Should().Be("Twelve children came.");

            var shared = await db.Set<CommunityInitiative>().AsNoTracking()
                .SingleAsync(row => row.Id == CommunityInitiativeId.From(world.SharedInitiativeId));
            shared.OrganiserIds.Should().Equal(world.OwnerId);

            var alone = await db.Set<CommunityInitiative>().AsNoTracking()
                .SingleAsync(row => row.Id == CommunityInitiativeId.From(world.SoleInitiativeId));
            alone.OrganiserIds.Should().BeEmpty();

            // No authorship stamp in these tables names the member any more — including the one the
            // erasure's own save would have written.
            foreach (var stamps in new[]
            {
                await db.Set<CommunityNotice>().AsNoTracking().Select(row => new[] { row.CreatedBy, row.ModifiedBy }).ToListAsync(),
                await db.Set<CommunityInitiative>().AsNoTracking().Select(row => new[] { row.CreatedBy, row.ModifiedBy }).ToListAsync(),
                await db.Set<CommunityInitiativeUpdate>().AsNoTracking().Select(row => new[] { row.CreatedBy, row.ModifiedBy }).ToListAsync(),
                await db.Set<CommunityRule>().AsNoTracking().Select(row => new[] { row.CreatedBy, row.ModifiedBy }).ToListAsync(),
            })
            {
                stamps.SelectMany(pair => pair).Should().NotContain(value => value != null && value.Contains(stamp));
            }
        }

        // The community still reads what it was told, with nobody named.
        var notices = await ReadDataAsync<List<NoticeDto>>(await world.Owner.GetAsync($"{Root}/{world.Slug}/notices"));
        notices.Should().ContainSingle(n => n.Id == world.NoticeId).Which.PublishedByName.Should().BeNull();

        var updates = await ReadDataAsync<List<UpdateDto>>(
            await world.Owner.GetAsync($"{Root}/{world.Slug}/initiatives/{world.SharedInitiativeId}/updates"));
        updates.Should().ContainSingle().Which.Author.Should().BeNull();

        // The initiative nobody organises any more is readable and the owner can still run it.
        var orphan = await ReadDataAsync<InitiativeDto>(
            await world.Owner.GetAsync($"{Root}/{world.Slug}/initiatives/{world.SoleInitiativeId}"));
        orphan.Organisers.Should().BeEmpty();
        orphan.CanEdit.Should().BeTrue();

        var renamed = await ReadDataAsync<InitiativeDto>(await world.Owner.PutAsJsonAsync(
            $"{Root}/{world.Slug}/initiatives/{world.SoleInitiativeId}",
            new { organiserIds = new[] { world.OwnerId } }));
        renamed.Organisers.Select(o => o.UserId).Should().Equal(world.OwnerId);

        // A second erasure finds nothing left to do.
        using (var scope = Factory.Services.CreateScope())
        {
            var provider = scope.ServiceProvider.GetServices<IDataErasureProvider>()
                .Single(candidate => candidate.Module == Module);

            var again = await provider.EraseAsync(world.MemberId);

            again.Where(result => result.Outcome != ErasureOutcome.Retained)
                .Should().OnlyContain(result => result.Outcome == ErasureOutcome.NothingHeld);
        }
    }

    /// <summary>
    /// <b>Export: what the member set, joined and wrote.</b> Not other people's names and not the
    /// counts the platform derived.
    /// </summary>
    [Fact]
    public async Task Export_includes_the_members_preferences_participations_and_what_they_wrote()
    {
        var world = await SeedAsync();

        using var scope = Factory.Services.CreateScope();

        var provider = scope.ServiceProvider.GetServices<IDataExportProvider>()
            .Should().ContainSingle(candidate => candidate.Module == Module).Subject;

        var sections = await provider.ExportAsync(world.MemberId);

        var preferences = Records(sections, "community preferences").Should().ContainSingle().Subject;
        preferences["preferredContentLanguage"].Should().Be("fr-CA");
        preferences["defaultMembershipVisibility"].Should().Be("CommunityOnly");

        var participation = Records(sections, "initiative participations").Should().ContainSingle().Subject;
        participation["initiativeId"].Should().Be(world.SharedInitiativeId);
        participation["initiativeTitle"].Should().Be("Language classes");

        var notice = Records(sections, "community notices published").Should().ContainSingle().Subject;
        notice["title"].Should().Be("Hall closed on Friday");
        notice["publishedAs"].Should().Be("Moderator");

        var update = Records(sections, "initiative updates posted").Should().ContainSingle().Subject;
        update["body"].Should().Be("Twelve children came.");
        update.Should().NotContainKey("authorId");

        var initiatives = Records(sections, "initiatives proposed or organised");
        initiatives.Select(row => row["initiativeId"])
            .Should().BeEquivalentTo(new object[] { world.SharedInitiativeId, world.SoleInitiativeId });
        initiatives.Should().OnlyContain(row => (bool)row["youOrganise"]!);
        initiatives.Should().OnlyContain(row => !row.ContainsKey("participantCount"));

        // And the platform's export carries the section, so the module is no longer listed as missing.
        var service = scope.ServiceProvider.GetRequiredService<IPersonalDataExportService>();
        var export = await service.ExportAsync(world.MemberId);
        export.ModulesWithoutExportProvider.Should().NotContain(Module);
        export.Sections.Should().Contain(section => section.Module == Module);
    }

    /// <summary>
    /// <b>The losing join, made deterministic.</b> The handler is given a repository that cannot see
    /// the row the winning request already wrote — exactly what the loser saw — so its insert hits
    /// the unique index. It answers the same success as a repeat join.
    /// <para>
    /// Not two parallel HTTP requests: this host shares one in-memory SQLite connection, and two
    /// requests using it at once is a concurrent use SQLite refuses, which would fail for a reason
    /// that has nothing to do with the race.
    /// </para>
    /// </summary>
    [Fact]
    public async Task A_join_that_loses_the_race_to_the_unique_index_answers_as_a_repeat_join()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative lost join");
        var (member, memberId) = await MemberAsync();
        await JoinCommunityAsync(member, slug);

        var initiative = await ReadDataAsync<InitiativeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives", new { title = "Lost join", description = "The second insert." }));

        // The winner.
        (await member.PostAsync($"{Root}/{slug}/initiatives/{initiative.Id}/participation", null))
            .EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var real = scope.ServiceProvider.GetRequiredService<ICommunityInitiativeRepository>();
        var blind = new RaceRepository(real) { HideParticipant = true };

        var handler = new JoinCommunityInitiativeCommandHandler(
            scope.ServiceProvider.GetRequiredService<ICommunityRepository>(),
            scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>(),
            blind,
            Access(scope, memberId),
            scope.ServiceProvider.GetRequiredService<IMemberProfileService>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

        var result = await handler.Handle(
            new JoinCommunityInitiativeCommand { IdOrSlug = slug, InitiativeId = initiative.Id },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsParticipant.Should().BeTrue();
        result.Value.ParticipantCount.Should().Be(1, "the member is counted once");

        var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
        db.ChangeTracker.Entries<CommunityInitiativeParticipant>()
            .Should().NotContain(entry => entry.State == EntityState.Added,
                "the refused row is let go, so nothing later in the scope writes it again");
    }

    /// <summary>
    /// <b>The losing leave, made deterministic.</b> The handler holds a row the winning request has
    /// already deleted, so its delete matches nothing. Leaving twice is leaving once.
    /// </summary>
    [Fact]
    public async Task A_leave_that_loses_the_race_answers_as_a_repeat_leave()
    {
        var (owner, _) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative lost leave");
        var (member, memberId) = await MemberAsync();
        await JoinCommunityAsync(member, slug);

        var initiative = await ReadDataAsync<InitiativeDto>(await owner.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives", new { title = "Lost leave", description = "The second delete." }));
        var path = $"{Root}/{slug}/initiatives/{initiative.Id}/participation";

        (await member.PostAsync(path, null)).EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var real = scope.ServiceProvider.GetRequiredService<ICommunityInitiativeRepository>();

        // What the loser loaded before the winner deleted it.
        var stale = await real.FindParticipantAsync(
            CommunityInitiativeId.From(initiative.Id), UserId.From(memberId));
        stale.Should().NotBeNull();

        // The winner.
        (await member.DeleteAsync(path)).EnsureSuccessStatusCode();

        var handler = new LeaveCommunityInitiativeCommandHandler(
            scope.ServiceProvider.GetRequiredService<ICommunityRepository>(),
            scope.ServiceProvider.GetRequiredService<ICommunityMembershipRepository>(),
            new RaceRepository(real) { Stale = stale },
            Access(scope, memberId),
            scope.ServiceProvider.GetRequiredService<IMemberProfileService>(),
            scope.ServiceProvider.GetRequiredService<IUnitOfWork>());

        var result = await handler.Handle(
            new LeaveCommunityInitiativeCommand { IdOrSlug = slug, InitiativeId = initiative.Id },
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsParticipant.Should().BeFalse();
        result.Value.ParticipantCount.Should().Be(0);
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────

    private sealed record World(
        HttpClient Owner,
        Guid OwnerId,
        Guid MemberId,
        string Slug,
        Guid NoticeId,
        Guid SharedInitiativeId,
        Guid SoleInitiativeId);

    /// <summary>
    /// A member with preferences, one participation, one notice, one update, and two initiatives
    /// they organise — one with the owner, one alone. Each through the route that writes it, except
    /// the notice: publishing one needs a moderator, and no route grants a role.
    /// </summary>
    private async Task<World> SeedAsync()
    {
        var (owner, ownerId) = await MemberAsync();
        var slug = await FoundAsync(owner, "Initiative privacy");

        var (member, memberId) = await MemberAsync();
        await JoinCommunityAsync(member, slug);
        (await member.PutAsJsonAsync($"{Root}/{slug}/membership/visibility", new { visibility = "CommunityOnly" }))
            .EnsureSuccessStatusCode();

        (await member.PutAsJsonAsync($"{Root}/preferences/me", new
        {
            crossCommunityDiscoveryEnabled = false,
            showPrimaryCommunityOnProfile = true,
            showInterestedCommunitiesOnProfile = false,
            showCommunityRolesOnProfile = false,
            preferredContentLanguage = "fr-CA",
            defaultMembershipVisibility = "CommunityOnly",
            notifications = Array.Empty<object>()
        })).EnsureSuccessStatusCode();

        var shared = await ReadDataAsync<InitiativeDto>(await owner.PostAsJsonAsync($"{Root}/{slug}/initiatives", new
        {
            title = "Language classes",
            description = "Weekly Yoruba classes for children.",
            organiserIds = new[] { ownerId, memberId }
        }));

        var sole = await ReadDataAsync<InitiativeDto>(await owner.PostAsJsonAsync($"{Root}/{slug}/initiatives", new
        {
            title = "Book drive",
            description = "Books for the school library.",
            organiserIds = new[] { memberId }
        }));

        // The member edits the one they lead, so a ModifiedBy stamp names them.
        (await member.PutAsJsonAsync($"{Root}/{slug}/initiatives/{sole.Id}", new { description = "Books, please." }))
            .EnsureSuccessStatusCode();

        (await member.PostAsync($"{Root}/{slug}/initiatives/{shared.Id}/participation", null))
            .EnsureSuccessStatusCode();

        await ReadDataAsync<UpdateDto>(await member.PostAsJsonAsync(
            $"{Root}/{slug}/initiatives/{shared.Id}/updates",
            new { title = "First class", body = "Twelve children came." }));

        Guid noticeId;

        using (var scope = Factory.Services.CreateScope())
        {
            var communities = scope.ServiceProvider.GetRequiredService<ICommunityRepository>();
            var community = await communities.FindByIdOrSlugAsync(slug);
            community.Should().NotBeNull();

            var notice = CommunityNotice.Publish(
                CommunityNoticeId.New(),
                community!.Id,
                UserId.From(memberId),
                CommunityMembershipRole.Moderator,
                "Hall closed on Friday",
                "The hall is closed for repairs.",
                expiresAt: null,
                isPinned: false,
                DateTimeOffset.UtcNow).Value;

            var db = scope.ServiceProvider.GetRequiredService<DiasporaDbContext>();
            db.Set<CommunityNotice>().Add(notice);
            await db.SaveChangesAsync();
            noticeId = notice.Id.Value;
        }

        return new World(owner, ownerId, memberId, slug, noticeId, shared.Id, sole.Id);
    }

    private static CommunityProgrammeAccessService Access(IServiceScope scope, Guid memberId) =>
        new(FakeCurrentUser.Authenticated(UserId.From(memberId)),
            scope.ServiceProvider.GetRequiredService<IOrganizationDirectory>());

    private static (ErasureOutcome, int) Outcome(IReadOnlyList<ErasureResult> results, string dataSet)
    {
        var result = results.Should().ContainSingle(candidate => candidate.DataSet == dataSet).Subject;
        return (result.Outcome, result.RecordsAffected);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> Records(
        IReadOnlyList<ExportSection> sections, string dataSet) =>
        sections.Should().ContainSingle(section => section.DataSet == dataSet).Subject.Records;

    // ── Helpers, as in CommunityInitiativeTests ─────────────────────────────────────────────

    private async Task<(HttpClient Client, Guid UserId)> MemberAsync()
    {
        var (_, auth) = await RegisterVerifiedMemberAsync();

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.Tokens.AccessToken);

        return (client, auth.User.Id);
    }

    private async Task<string> FoundAsync(HttpClient owner, string name)
    {
        var founded = await ReadDataAsync<MembershipDto>(await owner.PostAsJsonAsync(Root, new
        {
            name = $"{name} {Guid.NewGuid():N}"[..Math.Min(name.Length + 33, 100)],
            type = "NationalDiaspora",
            visibility = "Public",
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

    private static async Task JoinCommunityAsync(HttpClient member, string slug)
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

    /// <summary>
    /// The real repository, except for what the losing request of a race could see: no participant
    /// row (<see cref="HideParticipant"/>), or one it loaded before the winner deleted it
    /// (<see cref="Stale"/>).
    /// </summary>
    private sealed class RaceRepository(ICommunityInitiativeRepository inner) : ICommunityInitiativeRepository
    {
        public bool HideParticipant { get; init; }

        public CommunityInitiativeParticipant? Stale { get; init; }

        public Task<CommunityInitiativeParticipant?> FindParticipantAsync(
            CommunityInitiativeId initiativeId, UserId memberId, CancellationToken cancellationToken = default) =>
            HideParticipant
                ? Task.FromResult<CommunityInitiativeParticipant?>(null)
                : Stale is not null
                    ? Task.FromResult<CommunityInitiativeParticipant?>(Stale)
                    : inner.FindParticipantAsync(initiativeId, memberId, cancellationToken);

        public Task<IReadOnlyList<CommunityInitiative>> ListAsync(
            CommunityId communityId, CommunityInitiativeStatus? status, int limit, CancellationToken cancellationToken = default) =>
            inner.ListAsync(communityId, status, limit, cancellationToken);

        public Task<CommunityInitiative?> GetAsync(
            CommunityId communityId, CommunityInitiativeId id, CancellationToken cancellationToken = default) =>
            inner.GetAsync(communityId, id, cancellationToken);

        public void Add(CommunityInitiative initiative) => inner.Add(initiative);

        public Task<IReadOnlyDictionary<Guid, int>> CountParticipantsAsync(
            CommunityId communityId, IReadOnlyCollection<CommunityInitiativeId> initiativeIds, CancellationToken cancellationToken = default) =>
            inner.CountParticipantsAsync(communityId, initiativeIds, cancellationToken);

        public Task<IReadOnlyDictionary<Guid, int>> CountUpdatesAsync(
            IReadOnlyCollection<CommunityInitiativeId> initiativeIds, CancellationToken cancellationToken = default) =>
            inner.CountUpdatesAsync(initiativeIds, cancellationToken);

        public Task<IReadOnlySet<Guid>> ListJoinedAsync(
            UserId memberId, IReadOnlyCollection<CommunityInitiativeId> initiativeIds, CancellationToken cancellationToken = default) =>
            inner.ListJoinedAsync(memberId, initiativeIds, cancellationToken);

        public void AddParticipant(CommunityInitiativeParticipant participant) => inner.AddParticipant(participant);

        public void RemoveParticipant(CommunityInitiativeParticipant participant) => inner.RemoveParticipant(participant);

        public void ForgetParticipant(CommunityInitiativeParticipant participant) => inner.ForgetParticipant(participant);

        public Task<IReadOnlyList<CommunityInitiativeParticipant>> ListVisibleParticipantsAsync(
            CommunityId communityId, CommunityInitiativeId initiativeId, bool includeCommunityOnly, UserId? viewer, int limit,
            CancellationToken cancellationToken = default) =>
            inner.ListVisibleParticipantsAsync(communityId, initiativeId, includeCommunityOnly, viewer, limit, cancellationToken);

        public Task<IReadOnlyList<CommunityInitiativeUpdate>> ListUpdatesAsync(
            CommunityInitiativeId initiativeId, int limit, CancellationToken cancellationToken = default) =>
            inner.ListUpdatesAsync(initiativeId, limit, cancellationToken);

        public void AddUpdate(CommunityInitiativeUpdate update) => inner.AddUpdate(update);

        public Task<IReadOnlyList<CommunityMembership>> ListMembershipsAsync(
            CommunityId communityId, IReadOnlyCollection<UserId> memberIds, CancellationToken cancellationToken = default) =>
            inner.ListMembershipsAsync(communityId, memberIds, cancellationToken);
    }

    // ── Wire shapes ─────────────────────────────────────────────────────────────────────────

    private sealed record EnvelopeDto<T>(T? Data);

    private sealed record MembershipDto(Guid CommunityId, string State, string Visibility);

    private sealed record OrganiserDto(Guid UserId, string DisplayName, string Role);

    private sealed record InitiativeDto(
        Guid Id,
        List<OrganiserDto> Organisers,
        int ParticipantCount,
        bool IsParticipant,
        bool CanEdit);

    private sealed record AuthorDto(Guid UserId, string DisplayName);

    private sealed record UpdateDto(Guid Id, string Title, string Body, AuthorDto? Author);

    private sealed record NoticeDto(Guid Id, string Title, string? PublishedByName);
}
