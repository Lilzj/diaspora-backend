using Diaspora.Modules.Housing.Domain.Identifiers;

namespace Diaspora.Modules.Housing.Domain.Catalog;

/// <summary>How the module reaches its amenity catalogue.</summary>
public interface IHousingAmenityRepository
{
    Task<HousingAmenity?> GetAsync(
        HousingAmenityId id, CancellationToken cancellationToken = default);

    Task<HousingAmenity?> GetByKeyAsync(
        string key, CancellationToken cancellationToken = default);

    /// <summary>The whole catalogue. Read whole for the same reason the type catalogue is.</summary>
    Task<IReadOnlyList<HousingAmenity>> GetAllAsync(
        bool includeInactive = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// The keys a listing may claim, as a set. What the amenity validator checks against, and a
    /// set rather than a list because it is asked forty times per listing write.
    /// </summary>
    Task<IReadOnlySet<string>> GetActiveKeysAsync(
        CancellationToken cancellationToken = default);

    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(HousingAmenity amenity, CancellationToken cancellationToken = default);
}
