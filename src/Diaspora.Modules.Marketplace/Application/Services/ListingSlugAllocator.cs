using System.Globalization;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Services;

/// <summary>
/// Finds a free deep-link handle.
/// <para>
/// <b>Uniqueness is checked against deleted rows too, and that is deliberate.</b> A deleted listing
/// keeps its slug; handing it to a new listing would silently repoint every link ever shared to the
/// old one — including the ones in messages, in search engines and in somebody's saved list.
/// </para>
/// <para>
/// <b>A collision is resolved by suffixing, not by refusing.</b> Two members selling an "iPhone 13
/// Pro" in the same week is the ordinary case, not an error worth stopping them over. A caller who
/// asked for a specific handle and cannot have it <em>is</em> refused, because they asked for that
/// one.
/// </para>
/// </summary>
public sealed class ListingSlugAllocator
{
    private const int MaxAttempts = 25;

    private readonly IMarketplaceListingRepository _listings;
    private readonly ISellerProfileRepository _sellers;

    public ListingSlugAllocator(
        IMarketplaceListingRepository listings, ISellerProfileRepository sellers)
    {
        _listings = listings;
        _sellers = sellers;
    }

    /// <summary>
    /// A free handle for a listing.
    /// <para>
    /// <paramref name="requested"/> is honoured exactly when it is free and refused when it is not.
    /// Otherwise one is built from the title and the place, which is what makes a shared link
    /// readable — <c>iphone-13-pro-berlin</c> tells somebody what they are about to open.
    /// </para>
    /// </summary>
    public async Task<Result<ListingSlug>> AllocateListingSlugAsync(
        string? requested,
        string title,
        string? cityName,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var explicitSlug = ListingSlug.Create(requested);

            if (explicitSlug.IsFailure)
            {
                return explicitSlug;
            }

            return await _listings.SlugExistsAsync(explicitSlug.Value, cancellationToken)
                ? Result.Failure<ListingSlug>(MarketplaceErrors.SlugTaken)
                : explicitSlug;
        }

        var seed = ListingSlug.Slugify(title, cityName);

        if (seed.Length == 0)
        {
            return Result.Failure<ListingSlug>(MarketplaceErrors.SlugInvalid);
        }

        return await FindFreeAsync(
            seed,
            async (candidate, token) => await _listings.SlugExistsAsync(candidate, token),
            cancellationToken);
    }

    /// <summary>A free handle for a storefront. Same rules, different table.</summary>
    public async Task<Result<ListingSlug>> AllocateSellerSlugAsync(
        string? requested, string displayName, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var explicitSlug = ListingSlug.Create(requested);

            if (explicitSlug.IsFailure)
            {
                return explicitSlug;
            }

            return await _sellers.SlugExistsAsync(explicitSlug.Value, cancellationToken)
                ? Result.Failure<ListingSlug>(MarketplaceErrors.SellerSlugTaken)
                : explicitSlug;
        }

        var seed = ListingSlug.Slugify(displayName);

        if (seed.Length == 0)
        {
            return Result.Failure<ListingSlug>(MarketplaceErrors.SlugInvalid);
        }

        return await FindFreeAsync(
            seed,
            async (candidate, token) => await _sellers.SlugExistsAsync(candidate, token),
            cancellationToken);
    }

    /// <summary>
    /// Walks numeric suffixes until one is free.
    /// <para>
    /// Bounded at twenty-five attempts. Past that the seed is so contested that a numeric suffix
    /// stops being readable, and falling back to a short random tail gives a handle that is ugly
    /// and certain rather than pretty and unavailable.
    /// </para>
    /// </summary>
    private static async Task<Result<ListingSlug>> FindFreeAsync(
        string seed,
        Func<ListingSlug, CancellationToken, Task<bool>> exists,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = attempt == 0
                ? seed
                : $"{seed}-{attempt.ToString(CultureInfo.InvariantCulture)}";

            var slug = ListingSlug.Create(candidate);

            if (slug.IsFailure)
            {
                return slug;
            }

            if (!await exists(slug.Value, cancellationToken))
            {
                return slug;
            }
        }

        var fallback = ListingSlug.Create(
            $"{seed}-{Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..8]}");

        return fallback.IsFailure
            ? Result.Failure<ListingSlug>(MarketplaceErrors.SlugTaken)
            : fallback;
    }
}
