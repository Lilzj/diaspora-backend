using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Finds a web address nothing else is using.
/// <para>
/// <b>Slugs are never reused, including by deleted rows.</b> A link to a property is shared in
/// emails between people arranging to move, and handing a retired address to a new listing would
/// send somebody to the wrong home. The repositories therefore check with the soft-delete filter
/// off.
/// </para>
/// <para>
/// Collisions are resolved with a short suffix rather than by refusing: two landlords both
/// listing "Bright two-bed flat near the station" is an ordinary Tuesday, and neither should have
/// to invent a different title to be allowed to publish.
/// </para>
/// </summary>
public sealed class HousingSlugAllocator
{
    /// <summary>
    /// How many suffixes to try before falling back to a full identifier. Six is far more than
    /// real collision rates need and cheap enough not to matter when it is not.
    /// </summary>
    private const int MaxAttempts = 6;

    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;

    public HousingSlugAllocator(
        IHousingListingRepository listings, ILandlordProfileRepository landlords)
    {
        _listings = listings;
        _landlords = landlords;
    }

    /// <summary>Allocates a listing slug from its title.</summary>
    public async Task<HousingSlug> ForListingAsync(
        string? title, CancellationToken cancellationToken = default) =>
        await AllocateAsync(
            title,
            async (slug, token) => await _listings.SlugExistsAsync(slug, token),
            cancellationToken);

    /// <summary>Allocates a landlord slug from a display name.</summary>
    public async Task<HousingSlug> ForLandlordAsync(
        string? displayName, CancellationToken cancellationToken = default) =>
        await AllocateAsync(
            displayName,
            async (slug, token) => await _landlords.SlugExistsAsync(slug, token),
            cancellationToken);

    private static async Task<HousingSlug> AllocateAsync(
        string? source,
        Func<HousingSlug, CancellationToken, Task<bool>> exists,
        CancellationToken cancellationToken)
    {
        var candidate = HousingSlug.Create(source);

        if (!await exists(candidate, cancellationToken))
        {
            return candidate;
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // Six hex characters of a fresh identifier. Short enough to keep the address readable,
            // wide enough that two attempts colliding is not something anybody will see.
            var suffix = Guid.NewGuid().ToString("N")[..6];
            var suffixed = HousingSlug.Create(source, suffix);

            if (!await exists(suffixed, cancellationToken))
            {
                return suffixed;
            }
        }

        // Every attempt collided, which effectively cannot happen. A whole identifier is ugly and
        // certainly unique, and an ugly address is better than a refused listing.
        return HousingSlug.Create(source, Guid.NewGuid().ToString("N"));
    }
}
