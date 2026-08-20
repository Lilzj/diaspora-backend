using Diaspora.Modules.Housing.Domain.Identifiers;

namespace Diaspora.Modules.Housing.Domain.Catalog;

/// <summary>How the module reaches its accommodation-type catalogue.</summary>
public interface IAccommodationTypeRepository
{
    Task<AccommodationType?> GetAsync(
        AccommodationTypeId id, CancellationToken cancellationToken = default);

    /// <summary>By key, which is what listings store. The lookup every create makes.</summary>
    Task<AccommodationType?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// The whole catalogue. Small by construction - a few dozen rows - so it is read whole rather
    /// than paged, and cached per request by whichever resolver asked.
    /// </summary>
    Task<IReadOnlyList<AccommodationType>> GetAllAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(AccommodationType type, CancellationToken cancellationToken = default);
}
