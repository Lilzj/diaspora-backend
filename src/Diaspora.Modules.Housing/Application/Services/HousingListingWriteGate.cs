using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Loads a listing the caller is allowed to write to.
/// <para>
/// <b>Every write handler in the module starts here.</b> Each does the same two things before it
/// does anything interesting — find the listing, confirm the caller may act on it — and written out
/// at a dozen call sites it would be written correctly at eleven. The twelfth would be a member
/// editing somebody else's property.
/// </para>
/// <para>
/// A missing listing answers not-found; a listing belonging to somebody else answers forbidden
/// with the not-owned code, from <see cref="HousingAuthorizationService.RequireListingAccessAsync"/>.
/// That includes platform staff, who act on a member's listing only through the moderation routes
/// (7 Nov 2026).
/// </para>
/// </summary>
public sealed class HousingListingWriteGate
{
    private readonly IHousingListingRepository _listings;
    private readonly HousingAuthorizationService _authorization;

    public HousingListingWriteGate(
        IHousingListingRepository listings, HousingAuthorizationService authorization)
    {
        _listings = listings;
        _authorization = authorization;
    }

    /// <summary>The listing, loaded as a full graph and confirmed writable by this caller.</summary>
    public async Task<Result<HousingListing>> LoadAsync(
        Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetAsync(
            HousingListingId.From(listingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<HousingListing>(HousingErrors.ListingNotFound);
        }

        var access = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        return access.IsFailure
            ? Result.Failure<HousingListing>(access.Error)
            : Result.Success(listing);
    }

    /// <summary>
    /// The same, including soft-deleted rows.
    /// <para>
    /// Required by every restore path. The global query filter hides the row an undelete has to
    /// load, so a restore written against <see cref="LoadAsync"/> finds nothing and reports the
    /// listing missing — a defect the platform has recorded before, and one that looks exactly like
    /// the feature simply not working.
    /// </para>
    /// </summary>
    public async Task<Result<HousingListing>> LoadIncludingDeletedAsync(
        Guid listingId, CancellationToken cancellationToken = default)
    {
        var listing = await _listings.GetIncludingDeletedAsync(
            HousingListingId.From(listingId), cancellationToken);

        if (listing is null)
        {
            return Result.Failure<HousingListing>(HousingErrors.ListingNotFound);
        }

        var access = await _authorization.RequireListingAccessAsync(listing, cancellationToken);

        return access.IsFailure
            ? Result.Failure<HousingListing>(access.Error)
            : Result.Success(listing);
    }
}
