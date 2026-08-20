using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// EF Core repository for seller profiles.
/// <para>
/// No graph shapes here: the aggregate owns only value objects, so EF loads it whole and there is
/// nothing to include. What varies is tracking, which is what separates a command's read from a
/// results page's.
/// </para>
/// </summary>
public sealed class SellerProfileRepository : ISellerProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SellerProfileRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<SellerProfile> Sellers => _dbContext.Set<SellerProfile>();

    public async Task<SellerProfile?> GetAsync(
        SellerProfileId id, CancellationToken cancellationToken = default) =>
        await Sellers.FirstOrDefaultAsync(seller => seller.Id == id, cancellationToken);

    public async Task<SellerProfile?> GetForReadAsync(
        SellerProfileId id, CancellationToken cancellationToken = default) =>
        await Sellers.AsNoTracking()
            .FirstOrDefaultAsync(seller => seller.Id == id, cancellationToken);

    public async Task<SellerProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return await Sellers.FirstOrDefaultAsync(
            seller => seller.OwnerUserId == userId, cancellationToken);
    }

    public async Task<SellerProfile?> GetBySlugAsync(
        ListingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await Sellers.AsNoTracking()
            .FirstOrDefaultAsync(seller => seller.Slug.Value == value, cancellationToken);
    }

    public async Task<SellerProfile?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default) =>
        await Sellers.FirstOrDefaultAsync(
            seller => seller.TrustMark.VerificationId == verificationId, cancellationToken);

    /// <summary>
    /// Profiles for a set of identifiers, keyed.
    /// <para>
    /// The read every results page makes: twenty cards need twenty seller badges, and a query per
    /// card is the shape that makes a listing page slow in a way no index can fix.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<SellerProfileId, SellerProfile>> GetManyAsync(
        IReadOnlyCollection<SellerProfileId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return new Dictionary<SellerProfileId, SellerProfile>();
        }

        var keys = ids.Distinct().ToList();

        var rows = await Sellers.AsNoTracking()
            .Where(seller => keys.Contains(seller.Id))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(seller => seller.Id);
    }

    public async Task<bool> ExistsForUserAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Ignores the soft-delete filter: a member who closed a profile still has one, and letting
        // them open a second would split their sales history in half.
        return await Sellers.IgnoreQueryFilters()
            .AnyAsync(seller => seller.OwnerUserId == userId, cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        ListingSlug slug, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(slug);

        var value = slug.Value;

        return await Sellers.IgnoreQueryFilters()
            .AnyAsync(seller => seller.Slug.Value == value, cancellationToken);
    }

    public async Task<SellerProfile?> GetByProviderAsync(
        Guid providerId, CancellationToken cancellationToken = default) =>
        providerId == Guid.Empty
            ? null
            : await Sellers.AsNoTracking()
                .FirstOrDefaultAsync(seller => seller.ProviderId == providerId, cancellationToken);

    public async Task<CursorPagedResult<SellerProfile>> QueryAsync(
        SellerQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Sellers.AsNoTracking().AsQueryable();

        if (!filter.IncludeInactive)
        {
            query = query.Where(seller => seller.Status == SellerStatus.Active);
        }
        else if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(seller => statuses.Contains(seller.Status));
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(seller => seller.PrimaryCountryCode == country);
        }

        if (filter.Kind is { } kind)
        {
            query = query.Where(seller => seller.Identity.Kind == kind);
        }

        if (filter.VerifiedOnly)
        {
            query = query.Where(seller =>
                seller.TrustMark.State == ListingVerificationState.Verified);
        }

        if (filter.StorefrontsOnly)
        {
            query = query.Where(seller => seller.ProviderId != null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var term = filter.Text.Trim();
            query = query.Where(seller =>
                EF.Functions.Like(seller.Identity.DisplayName, $"%{term}%"));
        }

        query = ApplyCursor(query, MarketplaceCursor.Parse(cursor));

        var effectiveLimit = Math.Clamp(limit, 1, MarketplaceLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? MarketplaceCursor.FromTimestamp(rows[^1].CreatedAt, rows[^1].Slug.Value).Encode()
            : null;

        return CursorPagedResult<SellerProfile>.Create(rows, next, hasMore);
    }

    public async Task AddAsync(
        SellerProfile profile, CancellationToken cancellationToken = default) =>
        await Sellers.AddAsync(profile, cancellationToken);

    /// <summary>
    /// Newest first, tie-broken on the slug — unique, indexed and a string, because the primary key
    /// is a value-converted identifier EF cannot compare in SQL.
    /// </summary>
#pragma warning disable CA1309 // EF cannot translate the Ordinal overload. See the listing repository.
    private static IQueryable<SellerProfile> ApplyCursor(
        IQueryable<SellerProfile> query, MarketplaceCursor? cursor)
    {
        if (cursor?.AsTimestamp() is { } value)
        {
            query = query.Where(seller =>
                seller.CreatedAt < value
                || (seller.CreatedAt == value
                    && string.Compare(seller.Slug.Value, cursor!.LastKey) > 0));
        }

        return query
            .OrderByDescending(seller => seller.CreatedAt)
            .ThenBy(seller => seller.Slug.Value);
    }
#pragma warning restore CA1309
}
