using Diaspora.Modules.Media.Domain.Enums;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.ValueObjects;

namespace Diaspora.Modules.Media.Domain.Collections;

/// <summary>The media collection's persistence boundary.</summary>
public interface IMediaCollectionRepository
{
    /// <summary>One collection with its placements.</summary>
    Task<MediaCollection?> GetAsync(
        MediaCollectionId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The collection of a given kind belonging to an owning entity.
    /// <para>
    /// The lookup a vertical actually makes: Housing asks for <em>the</em> gallery of listing
    /// <c>7f3c…</c>, not for a collection by identifier it would have to store separately.
    /// One per owner and kind, which the unique index enforces.
    /// </para>
    /// </summary>
    Task<MediaCollection?> GetForOwnerAsync(
        MediaOwner owner, MediaCollectionKind kind, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MediaCollection>> ListForOwnerAsync(
        MediaOwner owner, CancellationToken cancellationToken = default);

    Task AddAsync(MediaCollection collection, CancellationToken cancellationToken = default);

    void Remove(MediaCollection collection);
}
