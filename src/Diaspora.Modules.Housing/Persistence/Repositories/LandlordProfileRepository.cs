using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for landlord profiles.
/// <para>
/// The profile owns only value objects, so there is no graph to include and no card-versus-full
/// distinction to make — the difference between the read methods here is tracking, nothing more.
/// </para>
/// </summary>
public sealed class LandlordProfileRepository : ILandlordProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public LandlordProfileRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<LandlordProfile> Landlords => _dbContext.Set<LandlordProfile>();

    public async Task<LandlordProfile?> GetAsync(
        LandlordProfileId id, CancellationToken cancellationToken = default) =>
        await Landlords.FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<LandlordProfile?> GetForReadAsync(
        LandlordProfileId id, CancellationToken cancellationToken = default) =>
        await Landlords.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Id == id, cancellationToken);

    public async Task<LandlordProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Landlords.FirstOrDefaultAsync(
            profile => profile.OwnerUserId == userId, cancellationToken);
    }

    public async Task<LandlordProfile?> GetBySlugAsync(
        HousingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await Landlords.AsNoTracking()
            .FirstOrDefaultAsync(profile => profile.Slug.Value == value, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<LandlordProfileId, LandlordProfile>> GetManyAsync(
        IReadOnlyCollection<LandlordProfileId> ids,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return new Dictionary<LandlordProfileId, LandlordProfile>();
        }

        // Whole identifiers, not their inner Guids: the converter maps the id as a unit and EF
        // cannot translate a reach through it.
        var keys = ids.Distinct().ToList();

        var rows = await Landlords.AsNoTracking()
            .Where(profile => keys.Contains(profile.Id))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(profile => profile.Id);
    }

    public async Task<LandlordProfile?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default) =>
        await Landlords.FirstOrDefaultAsync(
            profile => profile.TrustMark.VerificationId == verificationId, cancellationToken);

    public async Task<CursorPagedResult<LandlordProfile>> QueryAsync(
        LandlordQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Landlords.AsNoTracking(), filter);
        var parsed = HousingCursor.Parse(cursor);

#pragma warning disable CA1309 // EF cannot translate the ordinal overload. See the listing repository.
        if (parsed is not null)
        {
            query = query.Where(profile =>
                string.Compare(profile.Slug.Value, parsed.LastKey) > 0);
        }

        query = query.OrderBy(profile => profile.Slug.Value);
#pragma warning restore CA1309

        var effectiveLimit = Math.Clamp(limit, 1, HousingLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? HousingCursor.Create(rows[^1].Slug.Value, rows[^1].Slug.Value).Encode()
            : null;

        return CursorPagedResult<LandlordProfile>.Create(rows, next, hasMore);
    }

    public async Task<bool> SlugExistsAsync(
        HousingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        // Ignores the soft-delete filter: a closed profile keeps its web address.
        return await Landlords.IgnoreQueryFilters()
            .AnyAsync(profile => profile.Slug.Value == value, cancellationToken);
    }

    public async Task<bool> ExistsForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Ignores the soft-delete filter, and deliberately: a member whose profile was closed must
        // not be able to open a second one, or two profiles would claim one person's listings.
        return await Landlords.IgnoreQueryFilters()
            .AnyAsync(profile => profile.OwnerUserId == userId, cancellationToken);
    }

    public async Task AddAsync(
        LandlordProfile profile, CancellationToken cancellationToken = default) =>
        await Landlords.AddAsync(profile, cancellationToken);

    private static IQueryable<LandlordProfile> Apply(
        IQueryable<LandlordProfile> query, LandlordQueryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(profile => profile.PrimaryCountryCode == country);
        }

        if (filter.Kinds is { Count: > 0 })
        {
            var kinds = filter.Kinds.ToList();
            query = query.Where(profile => kinds.Contains(profile.Identity.Kind));
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(profile => statuses.Contains(profile.Status));
        }
        else
        {
            // A directory shows landlords who can actually let. Suspended and closed profiles are
            // reachable by identifier, so a member with an existing reservation can still see who
            // they are dealing with.
            query = query.Where(profile => profile.Status == LandlordStatus.Active);
        }

        if (filter.VerifiedOnly)
        {
            query = query.Where(profile =>
                profile.TrustMark.State == HousingVerificationState.Verified);
        }

        if (filter.ProviderId is { } providerId)
        {
            query = query.Where(profile => profile.ProviderId == providerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var term = filter.Text.Trim();

            query = query.Where(profile =>
                EF.Functions.Like(profile.Identity.DisplayName, $"%{term}%")
                || (profile.Identity.LegalName != null
                    && EF.Functions.Like(profile.Identity.LegalName, $"%{term}%")));
        }

        return query;
    }
}
