using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Repositories;

/// <summary>
/// Reads and writes membership programmes.
/// <para>
/// <b>Every read that renders a page includes the tiers</b>, because a programme without them is a
/// header with nothing under it — and an <c>Include</c> forgotten here surfaces as an empty tier list
/// rather than as an error.
/// </para>
/// </summary>
public sealed class MembershipProgrammeRepository : IMembershipProgrammeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MembershipProgrammeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<MembershipProgramme> Programmes => _dbContext.Set<MembershipProgramme>();

    public async Task<MembershipProgramme?> GetAsync(
        MembershipProgrammeId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Programmes.FirstOrDefaultAsync(
            programme => programme.Id == id, cancellationToken);
    }

    public async Task<MembershipProgramme?> FindByOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        if (organizationId == Guid.Empty)
        {
            return null;
        }

        return await Programmes.FirstOrDefaultAsync(
            programme => programme.OrganizationId == organizationId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, MembershipProgramme>> FindManyByOrganizationAsync(
        IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizationIds);

        var ids = organizationIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, MembershipProgramme>();
        }

        var rows = await Programmes
            .AsNoTracking()
            .Where(programme => ids.Contains(programme.OrganizationId))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(programme => programme.OrganizationId);
    }

    public async Task<CursorPagedResult<MembershipProgramme>> SearchAsync(
        ProgrammeQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Programmes.AsNoTracking();

        // Visibility, applied in the query. A private programme belonging to a body the caller is
        // not in never enters the result set — see the filter's own documentation for why that is
        // different from filtering it out afterwards.
        if (filter.VisibleOrganizationIds is { } visible)
        {
            var visibleIds = visible.ToList();

            query = query.Where(programme =>
                programme.Visibility == ProgrammeVisibility.Public
                || visibleIds.Contains(programme.OrganizationId));
        }
        else
        {
            query = query.Where(programme => programme.Visibility == ProgrammeVisibility.Public);
        }

        if (filter.CandidateOrganizationIds is { } candidates)
        {
            var candidateIds = candidates.ToList();
            query = query.Where(programme => candidateIds.Contains(programme.OrganizationId));
        }

        if (filter.OpenOnly)
        {
            query = query.Where(programme => programme.IsOpen);
        }

        // The price band is a question about the tiers rather than about the programme, so it is a
        // sub-query over the owned collection. `Any` over an owned collection translates; a
        // projection into memory to answer it would page the wrong rows.
        query = filter.PriceBand?.ToLowerInvariant() switch
        {
            "free" => query.Where(programme =>
                programme.Tiers.Any(tier => tier.Price == null)),

            "paid" => query.Where(programme =>
                programme.Tiers.Any(tier => tier.Price != null)),

            _ => query
        };

        var cursor = CommunityProgrammeCursor.Parse(filter.Cursor);

        query = CursorPage.Apply(
            query,
            cursor,
            MembershipProgrammeId.From,
            programme => programme.CreatedAt,
            programme => programme.Id);

        query = query
            .OrderByDescending(programme => programme.CreatedAt)
            .ThenBy(programme => programme.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            cursor,
            Math.Clamp(filter.Limit, 1, CommunityProgrammeLimits.MaxPageSize),
            programme => programme.CreatedAt,
            programme => programme.Id.Value,
            cancellationToken);
    }

    public void Add(MembershipProgramme programme)
    {
        ArgumentNullException.ThrowIfNull(programme);

        Programmes.Add(programme);
    }
}

/// <summary>Reads and writes programme memberships.</summary>
public sealed class ProgrammeMembershipRepository : IProgrammeMembershipRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ProgrammeMembershipRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ProgrammeMembership> Memberships => _dbContext.Set<ProgrammeMembership>();

    public async Task<ProgrammeMembership?> GetAsync(
        ProgrammeMembershipId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Memberships.FirstOrDefaultAsync(
            membership => membership.Id == id, cancellationToken);
    }

    public async Task<ProgrammeMembership?> ReadAsync(
        ProgrammeMembershipId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Memberships
            .AsNoTracking()
            .FirstOrDefaultAsync(membership => membership.Id == id, cancellationToken);
    }

    public async Task<ProgrammeMembership?> FindCurrentAsync(
        Guid organizationId, UserId memberUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserId);

        if (organizationId == Guid.Empty)
        {
            return null;
        }

        return await Memberships.FirstOrDefaultAsync(
            membership => membership.OrganizationId == organizationId
                && membership.MemberUserId == memberUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProgrammeMembership>> FindForMemberAsync(
        UserId memberUserId, int limit = 50, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserId);

        return await Memberships
            .AsNoTracking()
            .Where(membership => membership.MemberUserId == memberUserId)
            .OrderByDescending(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<CursorPagedResult<ProgrammeMembership>> SearchAsync(
        MembershipRollFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Memberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == filter.OrganizationId);

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(membership => statuses.Contains(membership.Status));
        }

        if (filter.TierId is { } tierId)
        {
            query = query.Where(membership => membership.TierId == tierId);
        }

        var cursor = CommunityProgrammeCursor.Parse(filter.Cursor);

        query = CursorPage.Apply(
            query,
            cursor,
            ProgrammeMembershipId.From,
            membership => membership.CreatedAt,
            membership => membership.Id);

        query = query
            .OrderByDescending(membership => membership.CreatedAt)
            .ThenBy(membership => membership.Id);

        return await CursorPage.MaterialiseAsync(
            query,
            cursor,
            Math.Clamp(filter.Limit, 1, CommunityProgrammeLimits.MaxPageSize),
            membership => membership.CreatedAt,
            membership => membership.Id.Value,
            cancellationToken);
    }

    public async Task<ProgrammeMembership?> FindByApplicationIdempotencyKeyAsync(
        UserId memberUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Memberships.FirstOrDefaultAsync(
            membership => membership.MemberUserId == memberUserId
                && membership.ApplicationIdempotencyKey == normalized,
            cancellationToken);
    }

    public async Task<ProgrammeMembership?> FindByRenewalIdempotencyKeyAsync(
        UserId memberUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        return await Memberships.FirstOrDefaultAsync(
            membership => membership.MemberUserId == memberUserId
                && membership.RenewalIdempotencyKey == normalized,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<MembershipTierId, int>> CountByTierAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        // The statuses that occupy a place, listed here rather than asked of the aggregate, because
        // OccupiesCapacity is a computed property EF cannot translate. Keeping the two in step is
        // what the unit test over ProgrammeMembershipTransitions is for.
        var occupying = OccupyingStatuses();

        var rows = await Memberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId
                && occupying.Contains(membership.Status))
            .GroupBy(membership => membership.TierId)
            .Select(group => new { TierId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.TierId, row => row.Count);
    }

    public async Task<IReadOnlyDictionary<ProgrammeMembershipStatus, int>> CountByStatusAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var rows = await Memberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId)
            .GroupBy(membership => membership.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Status, row => row.Count);
    }

    public async Task<IReadOnlyList<ProgrammeMembership>> FindDueForRenewalAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await Memberships
            .Where(membership => membership.Status == ProgrammeMembershipStatus.Active
                && membership.RenewalDueAt != null
                && membership.RenewalDueAt <= asOf)
            .OrderBy(membership => membership.RenewalDueAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProgrammeMembership>> FindExpiredAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default)
    {
        var expirable = new[]
        {
            ProgrammeMembershipStatus.Active,
            ProgrammeMembershipStatus.RenewalDue
        };

        return await Memberships
            .Where(membership => expirable.Contains(membership.Status)
                && membership.ExpiresAt != null
                && membership.ExpiresAt <= asOf)
            .OrderBy(membership => membership.ExpiresAt)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> FindOrganizationsWithMembershipsAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await Memberships
            .AsNoTracking()
            .Select(membership => membership.OrganizationId)
            .Distinct()
            .Take(Math.Clamp(limit, 1, 1000))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DateTimeOffset>> FindJoinMomentsAsync(
        Guid organizationId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default) =>
        await Memberships
            .AsNoTracking()
            .Where(membership => membership.OrganizationId == organizationId
                && membership.JoinedAt != null
                && membership.JoinedAt >= from
                && membership.JoinedAt <= to)
            .Select(membership => membership.JoinedAt!.Value)
            .ToListAsync(cancellationToken);

    public void Add(ProgrammeMembership membership)
    {
        ArgumentNullException.ThrowIfNull(membership);

        Memberships.Add(membership);
    }

    /// <summary>
    /// The statuses that hold a place in a tier's capacity.
    /// <para>
    /// Duplicated from <c>ProgrammeMembershipTransitions.OccupiesCapacity</c> because a computed
    /// property cannot be translated into SQL. The unit suite asserts the two agree for all fourteen
    /// states, which is the only way a duplication like this stays honest.
    /// </para>
    /// </summary>
    public static ProgrammeMembershipStatus[] OccupyingStatuses() =>
    [
        ProgrammeMembershipStatus.Pending,
        ProgrammeMembershipStatus.Approved,
        ProgrammeMembershipStatus.PaymentPending,
        ProgrammeMembershipStatus.Active,
        ProgrammeMembershipStatus.RenewalDue,
        ProgrammeMembershipStatus.Suspended
    ];
}
