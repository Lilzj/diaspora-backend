using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Housing.Domain.Landlords;

/// <summary>How the module reaches its landlord profiles.</summary>
public interface ILandlordProfileRepository
{
    Task<LandlordProfile?> GetAsync(
        LandlordProfileId id, CancellationToken cancellationToken = default);

    Task<LandlordProfile?> GetForReadAsync(
        LandlordProfileId id, CancellationToken cancellationToken = default);

    /// <summary>The caller's own profile. The first question every write handler asks.</summary>
    Task<LandlordProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task<LandlordProfile?> GetBySlugAsync(
        HousingSlug slug, CancellationToken cancellationToken = default);

    /// <summary>Batched, so a page of listings resolves its landlords in one query.</summary>
    Task<IReadOnlyDictionary<LandlordProfileId, LandlordProfile>> GetManyAsync(
        IReadOnlyCollection<LandlordProfileId> ids,
        CancellationToken cancellationToken = default);

    /// <summary>The profile a Trust outcome correlates to.</summary>
    Task<LandlordProfile?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<LandlordProfile>> QueryAsync(
        LandlordQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Ignores the soft-delete filter: a closed profile keeps its web address.</summary>
    Task<bool> SlugExistsAsync(HousingSlug slug, CancellationToken cancellationToken = default);

    Task<bool> ExistsForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task AddAsync(LandlordProfile profile, CancellationToken cancellationToken = default);
}
