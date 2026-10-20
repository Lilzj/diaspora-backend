using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Repositories;

/// <summary>Reads and writes a community's rules — 21 Oct 2026.</summary>
public sealed class CommunityRuleRepository : ICommunityRuleRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityRuleRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityRule> Rules => _dbContext.Set<CommunityRule>();

    public async Task<IReadOnlyList<CommunityRule>> ListAsync(
        CommunityId communityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);

        // Tracked: the write handlers renumber the list they read.
        return await Rules
            .Where(rule => rule.CommunityId == communityId)
            .OrderBy(rule => rule.Position)
            .ThenBy(rule => rule.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<CommunityRule?> GetAsync(
        CommunityId communityId, CommunityRuleId id, CancellationToken cancellationToken = default) =>
        Rules.FirstOrDefaultAsync(
            rule => rule.CommunityId == communityId && rule.Id == id, cancellationToken);

    public Task<int> CountAsync(CommunityId communityId, CancellationToken cancellationToken = default) =>
        Rules.CountAsync(rule => rule.CommunityId == communityId, cancellationToken);

    public void Add(CommunityRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Rules.Add(rule);
    }

    public void Remove(CommunityRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        Rules.Remove(rule);
    }
}

/// <summary>Reads and writes a community's notices — 21 Oct 2026.</summary>
public sealed class CommunityNoticeRepository : ICommunityNoticeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityNoticeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityNotice> Notices => _dbContext.Set<CommunityNotice>();

    public async Task<IReadOnlyList<CommunityNotice>> ListAsync(
        CommunityId communityId,
        bool includeExpired,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityId);

        var query = Notices.AsNoTracking().Where(notice => notice.CommunityId == communityId);

        if (!includeExpired)
        {
            query = query.Where(notice => notice.ExpiresAt == null || notice.ExpiresAt > now);
        }

        return await query
            .OrderByDescending(notice => notice.IsPinned)
            .ThenByDescending(notice => notice.PublishedAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    public Task<CommunityNotice?> GetAsync(
        CommunityId communityId, CommunityNoticeId id, CancellationToken cancellationToken = default) =>
        Notices.FirstOrDefaultAsync(
            notice => notice.CommunityId == communityId && notice.Id == id, cancellationToken);

    public Task<int> CountPublishedAsync(
        CommunityId communityId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default) =>
        Notices.CountAsync(
            notice => notice.CommunityId == communityId
                && notice.PublishedAt >= from
                && notice.PublishedAt < until,
            cancellationToken);

    public void Add(CommunityNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        Notices.Add(notice);
    }

    public void Remove(CommunityNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        Notices.Remove(notice);
    }
}

/// <summary>Reads and writes a member's community preferences — 21 Oct 2026.</summary>
public sealed class CommunityMemberPreferencesRepository : ICommunityMemberPreferencesRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityMemberPreferencesRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityMemberPreferences> Preferences => _dbContext.Set<CommunityMemberPreferences>();

    public Task<CommunityMemberPreferences?> FindAsync(
        UserId memberId, CancellationToken cancellationToken = default) =>
        Preferences.FirstOrDefaultAsync(preferences => preferences.MemberId == memberId, cancellationToken);

    public void Add(CommunityMemberPreferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        Preferences.Add(preferences);
    }
}

/// <summary>
/// Counts over <c>community_memberships</c> for a community's statistics — 21 Oct 2026.
/// <para>
/// States are spelled out in every predicate: <c>IsCurrentMember</c> is a computed property and does
/// not translate.
/// </para>
/// </summary>
public sealed class CommunityMembershipStatistics : ICommunityMembershipStatistics
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityMembershipStatistics(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private IQueryable<CommunityMembership> Memberships =>
        _dbContext.Set<CommunityMembership>().AsNoTracking();

    public Task<int> CountCurrentMembersAsync(
        CommunityId communityId, CancellationToken cancellationToken = default) =>
        Memberships.CountAsync(
            membership => membership.CommunityId == communityId
                && (membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember),
            cancellationToken);

    public async Task<IReadOnlyList<DateTimeOffset>> ListJoinTimesAsync(
        CommunityId communityId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        var joined = await Memberships
            .Where(membership => membership.CommunityId == communityId
                && membership.JoinedAt != null
                && membership.JoinedAt >= from
                && membership.JoinedAt < until)
            .Select(membership => membership.JoinedAt!.Value)
            .ToListAsync(cancellationToken);

        return joined;
    }

    public Task<int> CountDeparturesAsync(
        CommunityId communityId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default) =>
        Memberships.CountAsync(
            membership => membership.CommunityId == communityId
                && (membership.State == CommunityMembershipState.Left
                    || membership.State == CommunityMembershipState.Revoked)
                && membership.EndedAt != null
                && membership.EndedAt >= from
                && membership.EndedAt < until,
            cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListCurrentMemberIdsAsync(
        CommunityId communityId, int limit, CancellationToken cancellationToken = default)
    {
        var members = await Memberships
            .Where(membership => membership.CommunityId == communityId
                && (membership.State == CommunityMembershipState.Member
                    || membership.State == CommunityMembershipState.VerifiedMember))
            .OrderBy(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id)
            .Take(Math.Max(0, limit))
            .Select(membership => membership.MemberId)
            .ToListAsync(cancellationToken);

        return [.. members.Select(member => member.Value)];
    }

    public async Task<IReadOnlyList<CommunityMembership>> ListRoleHoldersAsync(
        CommunityId communityId,
        string? role,
        bool includeCommunityOnly,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Memberships.Where(membership =>
            membership.CommunityId == communityId
            && (membership.State == CommunityMembershipState.Member
                || membership.State == CommunityMembershipState.VerifiedMember)
            && (membership.Visibility == CommunityMembershipVisibility.Public
                || (includeCommunityOnly
                    && membership.Visibility == CommunityMembershipVisibility.CommunityOnly)));

        query = role is null
            // Any office at all. Member is implied by belonging and never stored, so a row with no
            // roles holds none.
            ? query.Where(membership => membership.Roles.Any())
            : query.Where(membership => membership.Roles.Contains(role));

        return await query
            .OrderBy(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }
}
