using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Marketplace.Domain.Sellers;

/// <summary>How the application layer reaches seller profiles.</summary>
public interface ISellerProfileRepository
{
    Task<SellerProfile?> GetAsync(
        SellerProfileId id, CancellationToken cancellationToken = default);

    Task<SellerProfile?> GetForReadAsync(
        SellerProfileId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's own profile, by member identifier. The read every write handler starts with,
    /// because a command arrives with a member identity and acts on a seller identity.
    /// </summary>
    Task<SellerProfile?> GetByUserAsync(
        UserId userId, CancellationToken cancellationToken = default);

    Task<SellerProfile?> GetBySlugAsync(
        ListingSlug slug, CancellationToken cancellationToken = default);

    /// <summary>The seller a Trust outcome names, found by the case identifier it was stamped with.</summary>
    Task<SellerProfile?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    /// <summary>Profiles for a set of identifiers, keyed. What a results page hydrates its badges from.</summary>
    Task<IReadOnlyDictionary<SellerProfileId, SellerProfile>> GetManyAsync(
        IReadOnlyCollection<SellerProfileId> ids, CancellationToken cancellationToken = default);

    /// <summary>Whether the member already has a profile. One per member.</summary>
    Task<bool> ExistsForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    Task<bool> SlugExistsAsync(ListingSlug slug, CancellationToken cancellationToken = default);

    /// <summary>The seller profile a Providers storefront is linked to, if any.</summary>
    Task<SellerProfile?> GetByProviderAsync(
        Guid providerId, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<SellerProfile>> QueryAsync(
        SellerQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(SellerProfile profile, CancellationToken cancellationToken = default);
}
