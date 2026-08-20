using Diaspora.Modules.Media.Domain.Collections;
using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Media.Persistence.Repositories;

/// <summary>EF Core repository for media collections.</summary>
public sealed class MediaCollectionRepository : IMediaCollectionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public MediaCollectionRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<MediaCollection> Collections => _dbContext.Set<MediaCollection>();

    public async Task<MediaCollection?> GetAsync(
        MediaCollectionId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Collections
            .Include(collection => collection.Items)
            .FirstOrDefaultAsync(collection => collection.Id == id, cancellationToken);
    }

    /// <summary>
    /// The lookup a vertical actually makes: <em>the</em> gallery of this listing, rather than
    /// a collection by an identifier it would otherwise have to store alongside its own row.
    /// </summary>
    public async Task<MediaCollection?> GetForOwnerAsync(
        MediaOwner owner, MediaCollectionKind kind, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return await Collections
            .Include(collection => collection.Items)
            .FirstOrDefaultAsync(
                collection =>
                    collection.Owner.Type == owner.Type &&
                    collection.Owner.Id == owner.Id &&
                    collection.Kind == kind,
                cancellationToken);
    }

    public async Task<IReadOnlyList<MediaCollection>> ListForOwnerAsync(
        MediaOwner owner, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);

        return await Collections
            .Include(collection => collection.Items)
            .Where(collection =>
                collection.Owner.Type == owner.Type && collection.Owner.Id == owner.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        MediaCollection collection, CancellationToken cancellationToken = default) =>
        await Collections.AddAsync(collection, cancellationToken);

    public void Remove(MediaCollection collection) => Collections.Remove(collection);
}
