using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for memberships.
/// <para>
/// <b><see cref="FindCurrentAsync"/> is the busiest method in the module.</b> Every event write,
/// every membership decision, every media change and every members-only visibility check asks it, so
/// it is a single indexed seek on the unique (organization, member) pair and it deliberately does
/// not load the history.
/// </para>
/// <para>
/// Paging is on the timestamp with a <c>CursorBoundary</c> exclusion rather than on a slug, because
/// a membership has no slug and its identifier is value-converted. The boundary carries the
/// identifiers already returned; B1.3 exists because the naive strict comparison silently drops
/// every row sharing an instant, and the persistence interceptor stamps them in batches.
/// </para>
/// </summary>
public sealed class OrganizationMembershipRepository : IOrganizationMembershipRepository
{
    private readonly DiasporaDbContext _dbContext;

    public OrganizationMembershipRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<OrganizationMembership> Memberships =>
        _dbContext.Set<OrganizationMembership>();

    public async Task<OrganizationMembership?> GetAsync(
        OrganizationMembershipId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Memberships
            .Include(membership => membership.History)
            .FirstOrDefaultAsync(membership => membership.Id == id, cancellationToken);
    }

    public async Task<OrganizationMembership?> FindAsync(
        OrganizationId organizationId,
        UserId memberUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(memberUserId);

        return await Memberships
            .Include(membership => membership.History)
            .FirstOrDefaultAsync(
                membership => membership.OrganizationId == organizationId
                    && membership.MemberUserId == memberUserId,
                cancellationToken);
    }

    public async Task<OrganizationMembership?> FindCurrentAsync(
        OrganizationId organizationId,
        UserId memberUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(memberUserId);

        // Not tracked and no history: this answers an authorisation question on nearly every write
        // in the module, and loading a member's whole record to ask whether they are an
        // administrator would make every event edit pay for it.
        return await Memberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                membership => membership.OrganizationId == organizationId
                    && membership.MemberUserId == memberUserId
                    && membership.Status == MembershipStatus.Active,
                cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationMembership>> FindCurrentForMemberAsync(
        UserId memberUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserId);

        return await Memberships
            .AsNoTracking()
            .Where(membership => membership.MemberUserId == memberUserId
                && membership.Status == MembershipStatus.Active)
            .OrderByDescending(membership => membership.Role)
            .ThenByDescending(membership => membership.JoinedAt)
            .Take(Math.Clamp(limit, 1, CommunityLifeLimits.MaxPageSize))
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<OrganizationMembership>> SearchAsync(
        MembershipQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Memberships.AsNoTracking();

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(membership => membership.OrganizationId == organizationId);
        }

        if (filter.MemberUserId is { } memberUserId)
        {
            query = query.Where(membership => membership.MemberUserId == memberUserId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(membership => statuses.Contains(membership.Status));
        }

        if (filter.MinimumRole is { } role)
        {
            query = query.Where(membership => membership.Role >= role);
        }

        var parsed = CommunityLifeCursor.Parse(filter.Cursor);

        if (parsed?.AsTimestamp() is { } boundary)
        {
            // Strictly before the boundary instant, plus whatever remains of the tie group at it.
            // A plain `< boundary` drops every row sharing the instant, and the instant is shared
            // constantly. The identifier cannot be compared in SQL — it is value-converted — so the
            // cursor carries the ones already returned and the query excludes them.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, OrganizationMembershipId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(membership =>
                membership.CreatedAt < boundary
                || (membership.CreatedAt == boundary
                    && !exhausted
                    && !seen.Contains(membership.Id)));
        }

        // Pending requests first: an administrator opening the member list is nearly always there
        // to answer one, and burying them under two thousand active members is how requests go
        // unanswered for a fortnight.
        query = query
            .OrderByDescending(membership => membership.Status == MembershipStatus.Requested)
            .ThenByDescending(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id);

        var limit = Math.Clamp(filter.Limit, 1, CommunityLifeLimits.MaxPageSize);
        var rows = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? CommunityLifeCursor.FromTimestamp(
                    rows[^1].CreatedAt,
                    CursorBoundary.Key(
                        rows,
                        row => row.CreatedAt,
                        row => row.Id.Value,
                        parsed?.LastKey,
                        parsed?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<OrganizationMembership>.Create(rows, next, hasMore);
    }

    public async Task<IReadOnlyDictionary<MembershipStatus, int>> CountByStatusAsync(
        OrganizationId organizationId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);

        var rows = await Memberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId)
            .GroupBy(membership => membership.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Status, row => row.Count);
    }

    /// <summary>
    /// Current members only, oldest first.
    /// <para>
    /// <c>Active</c> is the only status that is "in" — see <c>MembershipStatus</c>. An invited or
    /// requested member has not joined, and a broadcast that reached one would be an organization
    /// messaging somebody who has not agreed to hear from it.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ListCurrentMemberPageAsync(
        OrganizationId organizationId,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);

        return await Memberships
            .AsNoTracking()
            .Where(membership =>
                membership.OrganizationId == organizationId &&
                membership.Status == MembershipStatus.Active)
            .OrderBy(membership => membership.JoinedAt)
            .ThenBy(membership => membership.Id)
            .Skip(skip)
            .Take(take)
            .Select(membership => membership.MemberUserId.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<OrganizationMembership?> FindOwnerAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);

        // Exactly equal to Owner, not `>=`. The count above uses a floor because it is asking
        // "how many can do at least this"; this is asking who holds the one role there is one of.
        return await Memberships
            .AsNoTracking()
            .FirstOrDefaultAsync(
                membership => membership.OrganizationId == organizationId
                    && membership.Status == MembershipStatus.Active
                    && membership.Role == MembershipRole.Owner,
                cancellationToken);
    }

    public async Task<int> CountByRoleAsync(
        OrganizationId organizationId,
        MembershipRole role,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationId);

        return await Memberships
            .AsNoTracking()
            .CountAsync(
                membership => membership.OrganizationId == organizationId
                    && membership.Status == MembershipStatus.Active
                    && membership.Role >= role,
                cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationMembership>> FindLapsedAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await Memberships
            .Where(membership => membership.Status == MembershipStatus.Active
                && membership.RenewsAt != null
                && membership.RenewsAt < asOf)
            .OrderBy(membership => membership.RenewsAt)
            .Take(Math.Clamp(batchSize, 1, 1000))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        OrganizationMembership membership, CancellationToken cancellationToken = default) =>
        await Memberships.AddAsync(membership, cancellationToken);

    public void Update(OrganizationMembership membership) => Memberships.Update(membership);
}
