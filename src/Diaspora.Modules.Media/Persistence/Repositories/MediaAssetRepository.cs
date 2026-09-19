using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Media.Persistence.Repositories;

/// <summary>
/// EF Core repository for media assets.
/// <para>
/// Two read shapes on purpose. <see cref="GetAsync"/> loads the variants and grants, because
/// every read path needs both — access is decided from the grants and content is served from a
/// variant, and splitting those into two round trips buys nothing.
/// <see cref="GetSummaryAsync"/> loads neither, for the write paths that touch neither: a
/// delete or an attach that dragged in twenty variant rows would be paying for data it
/// discards.
/// </para>
/// </summary>
public sealed class MediaAssetRepository : IMediaAssetRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MediaAssetRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<MediaAsset> Assets => _dbContext.Set<MediaAsset>();

    public async Task<MediaAsset?> GetAsync(MediaId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Assets
            .Include(asset => asset.Variants)
            .Include(asset => asset.Grants)
            .FirstOrDefaultAsync(asset => asset.Id == id, cancellationToken);
    }

    public async Task<MediaAsset?> GetSummaryAsync(
        MediaId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Assets.FirstOrDefaultAsync(asset => asset.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetManyAsync(
        IReadOnlyCollection<MediaId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        // One query for the whole set. This is the method a gallery of forty photographs calls,
        // and the alternative is forty round trips over a number a member chose.
        return await Assets
            .Include(asset => asset.Variants)
            .Include(asset => asset.Grants)
            .Where(asset => ids.Contains(asset.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MediaAsset>> GetByOwnerAsync(
        MediaOwner owner, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return await Assets
            .Include(asset => asset.Variants)
            .Include(asset => asset.Grants)
            .Where(asset => asset.Owner.Type == owner.Type && asset.Owner.Id == owner.Id)
            .OrderBy(asset => asset.Role)
            .ThenBy(asset => asset.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<MediaAsset>> ListForUserAsync(
        UserId userId,
        MediaKind? kind,
        PaginationRequest pagination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(pagination);

        var page = pagination.Sanitize();

        var query = Assets.Where(asset => asset.UploadedBy == userId);

        if (kind is not null)
        {
            query = query.Where(asset => asset.Kind == kind);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(asset => asset.Variants)
            .OrderByDescending(asset => asset.CreatedAt)
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToListAsync(cancellationToken);

        return PagedResult<MediaAsset>.Create(items, page.Page, page.PageSize, total);
    }

    public async Task<IReadOnlyList<MediaAsset>> FindByHashAsync(
        ContentHash hash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hash);

        return await Assets
            .Include(asset => asset.Variants)
            .Include(asset => asset.Grants)
            .Where(asset => asset.Hash == hash)
            .OrderBy(asset => asset.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Assets nothing ever claimed, older than the grace period.
    /// <para>
    /// The population a vertical creates when it uploads a photograph and then fails to save
    /// the listing that would own it. Without a sweep they accumulate silently and forever,
    /// because every one of those uploads succeeded and nothing else will ever look at them.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<MediaAsset>> FindOrphansAsync(
        DateTimeOffset olderThan, int limit, CancellationToken cancellationToken = default) =>
        await Assets
            .Where(asset =>
                asset.Owner.Type == MediaOwnerType.Unassigned &&
                asset.CreatedAt < olderThan &&
                asset.Status != MediaAssetStatus.Deleted)
            .OrderBy(asset => asset.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MediaAsset>> FindDeletedAsync(
        DateTimeOffset deletedBefore, int limit, CancellationToken cancellationToken = default) =>
        await Assets
            .IgnoreQueryFilters()
            .Where(asset => asset.IsDeleted && asset.DeletedAt < deletedBefore)
            .OrderBy(asset => asset.DeletedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The quarantine, as a staff queue reads it.
    /// <para>
    /// Filtered on the status rather than on the scan verdict, because the status is what actually
    /// stops the file being served — a scan that came back infected and a status that never moved
    /// would be a bug, and a queue keyed on the verdict would hide it behind rows that look worked.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<MediaAsset>> FindQuarantinedAsync(
        int limit, CancellationToken cancellationToken = default) =>
        await Assets
            .Where(asset => asset.Status == MediaAssetStatus.Quarantined)
            .OrderBy(asset => asset.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MediaAsset asset, CancellationToken cancellationToken = default) =>
        await Assets.AddAsync(asset, cancellationToken);

    public void Remove(MediaAsset asset) => Assets.Remove(asset);
}
