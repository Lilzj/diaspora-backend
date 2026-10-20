using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Repositories;

/// <summary>
/// Reads and writes a community's initiatives — 21 Oct 2026.
/// <para>
/// States are spelled out in every predicate over the roll: <c>IsCurrentMember</c> is a computed
/// property and does not translate.
/// </para>
/// </summary>
public sealed class CommunityInitiativeRepository : ICommunityInitiativeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityInitiativeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityInitiative> Initiatives => _dbContext.Set<CommunityInitiative>();

    private DbSet<CommunityInitiativeParticipant> Participants =>
        _dbContext.Set<CommunityInitiativeParticipant>();

    private DbSet<CommunityInitiativeUpdate> Updates => _dbContext.Set<CommunityInitiativeUpdate>();

    private IQueryable<CommunityMembership> CurrentMembers(CommunityId communityId) =>
        _dbContext.Set<CommunityMembership>()
            .AsNoTracking()
            .Where(membership => membership.CommunityId == communityId
                && (membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember));

    public async Task<IReadOnlyList<CommunityInitiative>> ListAsync(
        CommunityId communityId,
        CommunityInitiativeStatus? status,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);

        var query = Initiatives.AsNoTracking().Where(initiative => initiative.CommunityId == communityId);

        if (status is { } wanted)
        {
            // Equality only. The status is stored as its name, so an ordering over it would sort words.
            query = query.Where(initiative => initiative.Status == wanted);
        }

        return await query
            .OrderByDescending(initiative => initiative.CreatedAt)
            .ThenBy(initiative => initiative.Id)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public Task<CommunityInitiative?> GetAsync(
        CommunityId communityId, CommunityInitiativeId id, CancellationToken cancellationToken = default) =>
        Initiatives.FirstOrDefaultAsync(
            initiative => initiative.CommunityId == communityId && initiative.Id == id, cancellationToken);

    public void Add(CommunityInitiative initiative)
    {
        ArgumentNullException.ThrowIfNull(initiative);
        Initiatives.Add(initiative);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountParticipantsAsync(
        CommunityId communityId,
        IReadOnlyCollection<CommunityInitiativeId> initiativeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initiativeIds);

        if (initiativeIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = initiativeIds.ToList();
        var members = CurrentMembers(communityId);

        var counts = await Participants.AsNoTracking()
            .Where(participant => ids.Contains(participant.InitiativeId)
                && members.Any(membership => membership.MemberId == participant.MemberId))
            .GroupBy(participant => participant.InitiativeId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(row => row.Key.Value, row => row.Count);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountUpdatesAsync(
        IReadOnlyCollection<CommunityInitiativeId> initiativeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initiativeIds);

        if (initiativeIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var ids = initiativeIds.ToList();

        var counts = await Updates.AsNoTracking()
            .Where(update => ids.Contains(update.InitiativeId))
            .GroupBy(update => update.InitiativeId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(row => row.Key.Value, row => row.Count);
    }

    public async Task<IReadOnlySet<Guid>> ListJoinedAsync(
        UserId memberId,
        IReadOnlyCollection<CommunityInitiativeId> initiativeIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberId);
        ArgumentNullException.ThrowIfNull(initiativeIds);

        if (initiativeIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = initiativeIds.ToList();

        var joined = await Participants.AsNoTracking()
            .Where(participant => participant.MemberId == memberId && ids.Contains(participant.InitiativeId))
            .Select(participant => participant.InitiativeId)
            .ToListAsync(cancellationToken);

        return joined.Select(id => id.Value).ToHashSet();
    }

    public Task<CommunityInitiativeParticipant?> FindParticipantAsync(
        CommunityInitiativeId initiativeId, UserId memberId, CancellationToken cancellationToken = default) =>
        Participants.FirstOrDefaultAsync(
            participant => participant.InitiativeId == initiativeId && participant.MemberId == memberId,
            cancellationToken);

    public void AddParticipant(CommunityInitiativeParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        Participants.Add(participant);
    }

    public void RemoveParticipant(CommunityInitiativeParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        Participants.Remove(participant);
    }

    public async Task<IReadOnlyList<CommunityInitiativeParticipant>> ListVisibleParticipantsAsync(
        CommunityId communityId,
        CommunityInitiativeId initiativeId,
        bool includeCommunityOnly,
        UserId? viewer,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);
        ArgumentNullException.ThrowIfNull(initiativeId);

        // The roll decides, as it does for role holders: Public to anybody who may read the
        // community, CommunityOnly to its current members, Private to nobody but the member.
        var visible = viewer is null
            ? CurrentMembers(communityId).Where(membership =>
                membership.Visibility == CommunityMembershipVisibility.Public
                || (includeCommunityOnly
                    && membership.Visibility == CommunityMembershipVisibility.CommunityOnly))
            : CurrentMembers(communityId).Where(membership =>
                membership.Visibility == CommunityMembershipVisibility.Public
                || (includeCommunityOnly
                    && membership.Visibility == CommunityMembershipVisibility.CommunityOnly)
                || membership.MemberId == viewer);

        return await Participants.AsNoTracking()
            .Where(participant => participant.InitiativeId == initiativeId
                && visible.Any(membership => membership.MemberId == participant.MemberId))
            .OrderBy(participant => participant.JoinedAt)
            .ThenBy(participant => participant.Id)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityInitiativeUpdate>> ListUpdatesAsync(
        CommunityInitiativeId initiativeId, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(initiativeId);

        return await Updates.AsNoTracking()
            .Where(update => update.InitiativeId == initiativeId)
            .OrderByDescending(update => update.PostedAt)
            .ThenBy(update => update.Id)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public void AddUpdate(CommunityInitiativeUpdate update)
    {
        ArgumentNullException.ThrowIfNull(update);
        Updates.Add(update);
    }

    public async Task<IReadOnlyList<CommunityMembership>> ListMembershipsAsync(
        CommunityId communityId,
        IReadOnlyCollection<UserId> memberIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberIds);

        if (memberIds.Count == 0)
        {
            return [];
        }

        var ids = memberIds.Distinct().ToList();

        return await _dbContext.Set<CommunityMembership>()
            .AsNoTracking()
            .Where(membership => membership.CommunityId == communityId && ids.Contains(membership.MemberId))
            .ToListAsync(cancellationToken);
    }
}
