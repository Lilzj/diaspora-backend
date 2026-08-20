using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Neighborhoods;

/// <summary>
/// How the module reaches its neighbourhood profiles.
/// <para>
/// <b>Almost every lookup here is by B4's area identifier rather than by the profile's own.</b>
/// That is the shape the whole design implies: callers know which place they mean, and the profile
/// is a thing hanging off that place. A caller that had to resolve a profile identifier first
/// would be a caller that had to know Housing owns profiles at all.
/// </para>
/// </summary>
public interface INeighborhoodProfileRepository
{
    Task<NeighborhoodProfile?> GetAsync(
        NeighborhoodProfileId id, CancellationToken cancellationToken = default);

    /// <summary>The profile for a B4 area. The lookup nearly every caller makes.</summary>
    Task<NeighborhoodProfile?> GetByAreaAsync(
        Guid areaId, CancellationToken cancellationToken = default);

    Task<NeighborhoodProfile?> GetByAreaForReadAsync(
        Guid areaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// By slug, optionally within a country. The client's neighbourhood route addresses places by
    /// slug, and a slug is unique within its parent rather than globally - there is a Springfield
    /// in twelve states, and B4 records exactly that.
    /// </summary>
    Task<NeighborhoodProfile?> GetBySlugAsync(
        string slug, string? countryCode = null, CancellationToken cancellationToken = default);

    /// <summary>Batched, so a page of listings resolves its neighbourhoods in one query.</summary>
    Task<IReadOnlyDictionary<Guid, NeighborhoodProfile>> GetManyByAreaAsync(
        IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<NeighborhoodProfile>> QueryAsync(
        NeighborhoodQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForAreaAsync(Guid areaId, CancellationToken cancellationToken = default);

    Task AddAsync(NeighborhoodProfile profile, CancellationToken cancellationToken = default);
}
