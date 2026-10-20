using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.Domain.Abstractions;
using Diaspora.Persistence;
using Diaspora.Persistence.Atomic;
using Diaspora.Platform.Operations.Privacy;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Infrastructure.Privacy;

/// <summary>The module's name in a privacy receipt, and the data sets it reports.</summary>
internal static class CommunityProgrammePrivacy
{
    public const string Module = CommunityProgrammeModuleMetadata.Name;

    public const string PreferencesDataSet = "community preferences";
    public const string ParticipationsDataSet = "initiative participations";
    public const string NoticesDataSet = "community notices published";
    public const string UpdatesDataSet = "initiative updates posted";
    public const string InitiativesDataSet = "initiatives proposed or organised";
    public const string AuditStampsDataSet = "authorship stamps on community rules, notices and initiatives";

    public const string MembershipsDataSet = "community memberships";
    public const string ProgrammeMembershipsDataSet = "programme memberships";
    public const string VolunteerApplicationsDataSet = "volunteer applications";
    public const string VolunteerAssignmentsDataSet = "volunteer assignments";

    /// <summary>
    /// Why the older data sets are reported as kept. Said in the receipt rather than left out of it,
    /// so registering this provider does not make the module read as fully covered.
    /// </summary>
    public const string NotYetCoveredReason =
        "Not yet covered by account erasure. A community membership can hold a community's ownership, "
        + "and removing it needs an ownership decision that has not been made; until it is, the record "
        + "is kept, and it names nobody once the account itself is anonymised.";
}

/// <summary>
/// Erases what a member left in a community's governance and initiatives — 21 Oct 2026.
/// <para>
/// <b>What is the member's own goes; what is the community's stays, without them in it.</b>
/// </para>
/// <list type="bullet">
///   <item>
///     <b>Preferences</b> (<c>community_member_preferences</c>) and <b>initiative participations</b>
///     (<c>community_initiative_participants</c>) are deleted. Both are facts about one person —
///     what they want to be told and what they take part in — and nothing else points at them.
///   </item>
///   <item>
///     <b>Notices</b> they published and <b>initiative updates</b> they posted are anonymised: the
///     author column becomes <see cref="UserId.Empty"/>. The text is the community's record — a
///     notice its members were told, a report on its own project — and deleting it would rewrite
///     the community's history. This is the platform's policy for authored content elsewhere:
///     the Identity provider anonymises the account and leaves what it wrote in place. Here the
///     link itself is cut as well, because these tables are this module's to cut. The notice keeps
///     its <c>PublishedByRole</c> ("Posted by a Moderator"), which is a role and names nobody.
///     Every reader already renders an author no membership resolves as "nobody named".
///   </item>
///   <item>
///     <b>Initiatives</b>: the member is removed from the organisers, and a proposer is replaced by
///     <see cref="UserId.Empty"/>. An initiative left with no organiser is kept and stays readable;
///     the community's managers (<c>CanManageInitiatives</c>) can still change it and name new
///     organisers. See <see cref="CommunityInitiative.ForgetMember"/>.
///   </item>
///   <item>
///     <b>Audit stamps</b> (<c>CreatedBy</c>/<c>ModifiedBy</c>) on rules, notices, initiatives and
///     updates hold the acting user's identifier as a string, so stamps naming the member are
///     cleared. This runs last, through <see cref="IAtomicUpdateExecutor"/> rather than the change
///     tracker, because a tracked save re-stamps <c>ModifiedBy</c> with whoever is running the
///     erasure — the member themself, when they close their own account.
///   </item>
/// </list>
/// <para>
/// <b>Not covered yet</b>, and reported as <see cref="ErasureOutcome.Retained"/> with the reason
/// rather than left out: community memberships, programme memberships and volunteering records.
/// Polls, organisation announcements and ownership transfers also hold user ids and are not
/// reported here.
/// </para>
/// </summary>
public sealed class CommunityProgrammeDataErasureProvider : IDataErasureProvider
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IAtomicUpdateExecutor _atomic;

    public CommunityProgrammeDataErasureProvider(DiasporaDbContext dbContext, IAtomicUpdateExecutor atomic)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(atomic);
        _dbContext = dbContext;
        _atomic = atomic;
    }

    public string Module => CommunityProgrammePrivacy.Module;

    public async Task<IReadOnlyList<ErasureResult>> PreviewAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var member = UserId.From(userId);
        var stamp = userId.ToString();

        var preferences = await _dbContext.Set<CommunityMemberPreferences>().IgnoreQueryFilters()
            .CountAsync(row => row.MemberId == member, cancellationToken);

        var participations = await _dbContext.Set<CommunityInitiativeParticipant>().IgnoreQueryFilters()
            .CountAsync(row => row.MemberId == member, cancellationToken);

        var notices = await _dbContext.Set<CommunityNotice>().IgnoreQueryFilters()
            .CountAsync(row => row.PublishedBy == member, cancellationToken);

        var updates = await _dbContext.Set<CommunityInitiativeUpdate>().IgnoreQueryFilters()
            .CountAsync(row => row.AuthorId == member, cancellationToken);

        var initiatives = await _dbContext.Set<CommunityInitiative>().IgnoreQueryFilters()
            .CountAsync(row => row.ProposedBy == member || row.OrganiserIds.Contains(userId), cancellationToken);

        var stamps = await CountStampsAsync(stamp, cancellationToken);

        return
        [
            Deleted(CommunityProgrammePrivacy.PreferencesDataSet, preferences),
            Deleted(CommunityProgrammePrivacy.ParticipationsDataSet, participations),
            Anonymized(CommunityProgrammePrivacy.NoticesDataSet, notices),
            Anonymized(CommunityProgrammePrivacy.UpdatesDataSet, updates),
            Anonymized(CommunityProgrammePrivacy.InitiativesDataSet, initiatives),
            Anonymized(CommunityProgrammePrivacy.AuditStampsDataSet, stamps),
            .. await NotYetCoveredAsync(member, cancellationToken),
        ];
    }

    public async Task<IReadOnlyList<ErasureResult>> EraseAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var member = UserId.From(userId);
        var stamp = userId.ToString();

        // Through the change tracker, as the platform requires of a module: set-based deletes and
        // updates are kept out of the verticals (PersistenceRuleArchitectureTests).
        var preferences = await _dbContext.Set<CommunityMemberPreferences>().IgnoreQueryFilters()
            .Where(row => row.MemberId == member)
            .ToListAsync(cancellationToken);

        var participations = await _dbContext.Set<CommunityInitiativeParticipant>().IgnoreQueryFilters()
            .Where(row => row.MemberId == member)
            .ToListAsync(cancellationToken);

        var notices = await _dbContext.Set<CommunityNotice>().IgnoreQueryFilters()
            .Where(row => row.PublishedBy == member)
            .ToListAsync(cancellationToken);

        var updates = await _dbContext.Set<CommunityInitiativeUpdate>().IgnoreQueryFilters()
            .Where(row => row.AuthorId == member)
            .ToListAsync(cancellationToken);

        var initiatives = await _dbContext.Set<CommunityInitiative>().IgnoreQueryFilters()
            .Where(row => row.ProposedBy == member || row.OrganiserIds.Contains(userId))
            .ToListAsync(cancellationToken);

        _dbContext.Set<CommunityMemberPreferences>().RemoveRange(preferences);
        _dbContext.Set<CommunityInitiativeParticipant>().RemoveRange(participations);
        notices.ForEach(notice => notice.ForgetPublisher());
        updates.ForEach(update => update.ForgetAuthor());
        var changed = initiatives.Count(initiative => initiative.ForgetMember(member));

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Last, and not through the tracker: the save above stamped ModifiedBy with whoever is
        // running the erasure (the member themself, when they close their own account), and any
        // tracked save would do it again.
        var stamps = await ScrubStampsAsync(stamp, cancellationToken);

        return
        [
            Deleted(CommunityProgrammePrivacy.PreferencesDataSet, preferences.Count),
            Deleted(CommunityProgrammePrivacy.ParticipationsDataSet, participations.Count),
            Anonymized(CommunityProgrammePrivacy.NoticesDataSet, notices.Count),
            Anonymized(CommunityProgrammePrivacy.UpdatesDataSet, updates.Count),
            Anonymized(CommunityProgrammePrivacy.InitiativesDataSet, changed),
            Anonymized(CommunityProgrammePrivacy.AuditStampsDataSet, stamps),
            .. await NotYetCoveredAsync(member, cancellationToken),
        ];
    }

    private async Task<int> CountStampsAsync(string stamp, CancellationToken cancellationToken) =>
        await _dbContext.Set<CommunityRule>().IgnoreQueryFilters()
            .CountAsync(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp), cancellationToken)
        + await _dbContext.Set<CommunityNotice>().IgnoreQueryFilters()
            .CountAsync(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp), cancellationToken)
        + await _dbContext.Set<CommunityInitiative>().IgnoreQueryFilters()
            .CountAsync(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp), cancellationToken)
        + await _dbContext.Set<CommunityInitiativeUpdate>().IgnoreQueryFilters()
            .CountAsync(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp), cancellationToken);

    /// <summary>
    /// Clears the stamps naming the member, one row at a time through the platform's
    /// <see cref="IAtomicUpdateExecutor"/>: a plain <c>col = NULL</c> by key that does not pass
    /// through the audit stamping a tracked save applies. A stamp naming somebody else is kept.
    /// </summary>
    private async Task<int> ScrubStampsAsync(string stamp, CancellationToken cancellationToken)
    {
        var rules = await _dbContext.Set<CommunityRule>().IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp))
            .Select(row => new { row.Id, row.CreatedBy, row.ModifiedBy })
            .ToListAsync(cancellationToken);

        var notices = await _dbContext.Set<CommunityNotice>().IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp))
            .Select(row => new { row.Id, row.CreatedBy, row.ModifiedBy })
            .ToListAsync(cancellationToken);

        var initiatives = await _dbContext.Set<CommunityInitiative>().IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp))
            .Select(row => new { row.Id, row.CreatedBy, row.ModifiedBy })
            .ToListAsync(cancellationToken);

        var updates = await _dbContext.Set<CommunityInitiativeUpdate>().IgnoreQueryFilters().AsNoTracking()
            .Where(row => row.CreatedBy!.Contains(stamp) || row.ModifiedBy!.Contains(stamp))
            .Select(row => new { row.Id, row.CreatedBy, row.ModifiedBy })
            .ToListAsync(cancellationToken);

        foreach (var row in rules)
        {
            await ScrubAsync<CommunityRule>(row.Id, row.CreatedBy, row.ModifiedBy, stamp, cancellationToken);
        }

        foreach (var row in notices)
        {
            await ScrubAsync<CommunityNotice>(row.Id, row.CreatedBy, row.ModifiedBy, stamp, cancellationToken);
        }

        foreach (var row in initiatives)
        {
            await ScrubAsync<CommunityInitiative>(row.Id, row.CreatedBy, row.ModifiedBy, stamp, cancellationToken);
        }

        foreach (var row in updates)
        {
            await ScrubAsync<CommunityInitiativeUpdate>(row.Id, row.CreatedBy, row.ModifiedBy, stamp, cancellationToken);
        }

        return rules.Count + notices.Count + initiatives.Count + updates.Count;
    }

    private async Task ScrubAsync<TEntity>(
        object id,
        string? createdBy,
        string? modifiedBy,
        string stamp,
        CancellationToken cancellationToken)
        where TEntity : class, IAuditableEntity
    {
        var update = AtomicUpdate.For<TEntity>(id);

        if (createdBy?.Contains(stamp, StringComparison.Ordinal) == true)
        {
            update.Set(entity => entity.CreatedBy, null);
        }

        if (modifiedBy?.Contains(stamp, StringComparison.Ordinal) == true)
        {
            update.Set(entity => entity.ModifiedBy, null);
        }

        await _atomic.ExecuteAsync(update, cancellationToken);

        // A tracked copy would still hold the stamp; nothing in this scope may write it back.
        foreach (var entry in _dbContext.ChangeTracker.Entries<TEntity>()
                     .Where(entry => Equals(entry.Property("Id").CurrentValue, id))
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    private async Task<IReadOnlyList<ErasureResult>> NotYetCoveredAsync(
        UserId member, CancellationToken cancellationToken)
    {
        var memberships = await _dbContext.Set<CommunityMembership>().IgnoreQueryFilters()
            .CountAsync(row => row.MemberId == member, cancellationToken);

        var programme = await _dbContext.Set<ProgrammeMembership>().IgnoreQueryFilters()
            .CountAsync(row => row.MemberUserId == member, cancellationToken);

        var applications = await _dbContext.Set<VolunteerApplication>().IgnoreQueryFilters()
            .CountAsync(row => row.VolunteerUserId == member, cancellationToken);

        var assignments = await _dbContext.Set<VolunteerAssignment>().IgnoreQueryFilters()
            .CountAsync(row => row.VolunteerUserId == member, cancellationToken);

        return
        [
            Retained(CommunityProgrammePrivacy.MembershipsDataSet, memberships),
            Retained(CommunityProgrammePrivacy.ProgrammeMembershipsDataSet, programme),
            Retained(CommunityProgrammePrivacy.VolunteerApplicationsDataSet, applications),
            Retained(CommunityProgrammePrivacy.VolunteerAssignmentsDataSet, assignments),
        ];
    }

    private static ErasureResult Deleted(string dataSet, int count) =>
        new(CommunityProgrammePrivacy.Module, dataSet,
            count == 0 ? ErasureOutcome.NothingHeld : ErasureOutcome.Deleted, count);

    private static ErasureResult Anonymized(string dataSet, int count) =>
        new(CommunityProgrammePrivacy.Module, dataSet,
            count == 0 ? ErasureOutcome.NothingHeld : ErasureOutcome.Anonymized, count);

    private static ErasureResult Retained(string dataSet, int count) =>
        count == 0
            ? new(CommunityProgrammePrivacy.Module, dataSet, ErasureOutcome.NothingHeld, 0)
            : new(CommunityProgrammePrivacy.Module, dataSet, ErasureOutcome.Retained, count,
                CommunityProgrammePrivacy.NotYetCoveredReason);
}

/// <summary>
/// Gives a member back what they put into community governance and initiatives — 21 Oct 2026.
/// <para>
/// What they contributed, which is the line the export contract draws: their preferences as they
/// set them, the initiatives they joined, and the notices, updates and initiatives they wrote or
/// lead. Not participant counts, not other people's names, and not the capability set the platform
/// derived for them.
/// </para>
/// </summary>
public sealed class CommunityProgrammeDataExportProvider : IDataExportProvider
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityProgrammeDataExportProvider(DiasporaDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    public string Module => CommunityProgrammePrivacy.Module;

    public async Task<IReadOnlyList<ExportSection>> ExportAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var member = UserId.From(userId);
        var sections = new List<ExportSection>();

        var preferences = await _dbContext.Set<CommunityMemberPreferences>().IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.MemberId == member, cancellationToken);

        if (preferences is not null)
        {
            sections.Add(Section(CommunityProgrammePrivacy.PreferencesDataSet,
            [
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["crossCommunityDiscoveryEnabled"] = preferences.CrossCommunityDiscoveryEnabled,
                    ["showPrimaryCommunityOnProfile"] = preferences.ShowPrimaryCommunityOnProfile,
                    ["showInterestedCommunitiesOnProfile"] = preferences.ShowInterestedCommunitiesOnProfile,
                    ["showCommunityRolesOnProfile"] = preferences.ShowCommunityRolesOnProfile,
                    ["defaultMembershipVisibility"] = preferences.DefaultMembershipVisibility.ToString(),
                    ["preferredContentLanguage"] = preferences.PreferredContentLanguage,
                    ["notifications"] = preferences.Notifications
                        .Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["categoryKey"] = row.CategoryKey,
                            ["scope"] = row.Scope,
                            ["communityIds"] = row.CommunityIds.ToList(),
                        })
                        .ToList(),
                    ["updatedAt"] = preferences.ModifiedAt ?? preferences.CreatedAt,
                },
            ]));
        }

        var participations = await (
                from participant in _dbContext.Set<CommunityInitiativeParticipant>().IgnoreQueryFilters()
                join initiative in _dbContext.Set<CommunityInitiative>().IgnoreQueryFilters()
                    on participant.InitiativeId equals initiative.Id
                where participant.MemberId == member
                orderby participant.JoinedAt
                select new { participant.CommunityId, participant.InitiativeId, initiative.Title, participant.JoinedAt })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        sections.Add(Section(CommunityProgrammePrivacy.ParticipationsDataSet,
        [
            .. participations.Select(row => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["communityId"] = row.CommunityId.Value,
                ["initiativeId"] = row.InitiativeId.Value,
                ["initiativeTitle"] = row.Title,
                ["joinedAt"] = row.JoinedAt,
            }),
        ]));

        var notices = await _dbContext.Set<CommunityNotice>().IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.PublishedBy == member)
            .OrderBy(row => row.PublishedAt)
            .ToListAsync(cancellationToken);

        sections.Add(Section(CommunityProgrammePrivacy.NoticesDataSet,
        [
            .. notices.Select(notice => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["noticeId"] = notice.Id.Value,
                ["communityId"] = notice.CommunityId.Value,
                ["title"] = notice.Title,
                ["body"] = notice.Body,
                ["publishedAs"] = notice.PublishedByRole,
                ["publishedAt"] = notice.PublishedAt,
                ["expiresAt"] = notice.ExpiresAt,
                ["isPinned"] = notice.IsPinned,
            }),
        ]));

        var updates = await _dbContext.Set<CommunityInitiativeUpdate>().IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.AuthorId == member)
            .OrderBy(row => row.PostedAt)
            .ToListAsync(cancellationToken);

        sections.Add(Section(CommunityProgrammePrivacy.UpdatesDataSet,
        [
            .. updates.Select(update => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["updateId"] = update.Id.Value,
                ["communityId"] = update.CommunityId.Value,
                ["initiativeId"] = update.InitiativeId.Value,
                ["title"] = update.Title,
                ["body"] = update.Body,
                ["postedAt"] = update.PostedAt,
            }),
        ]));

        var initiatives = await _dbContext.Set<CommunityInitiative>().IgnoreQueryFilters()
            .AsNoTracking()
            .Where(row => row.ProposedBy == member || row.OrganiserIds.Contains(userId))
            .OrderBy(row => row.CreatedAt)
            .ToListAsync(cancellationToken);

        sections.Add(Section(CommunityProgrammePrivacy.InitiativesDataSet,
        [
            .. initiatives.Select(initiative => new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["initiativeId"] = initiative.Id.Value,
                ["communityId"] = initiative.CommunityId.Value,
                ["title"] = initiative.Title,
                ["description"] = initiative.Description,
                ["status"] = initiative.Status.ToString(),
                ["targetDate"] = initiative.TargetDate,
                ["proposedByYou"] = initiative.ProposedBy == member,
                ["youOrganise"] = initiative.OrganiserIds.Contains(userId),
                ["createdAt"] = initiative.CreatedAt,
            }),
        ]));

        return sections;
    }

    private static ExportSection Section(
        string dataSet, IReadOnlyList<Dictionary<string, object?>> records) =>
        new(CommunityProgrammePrivacy.Module, dataSet, [.. records.Cast<IReadOnlyDictionary<string, object?>>()]);
}
