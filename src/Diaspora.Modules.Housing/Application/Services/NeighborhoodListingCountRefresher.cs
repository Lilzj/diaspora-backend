using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Neighborhoods;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Puts a neighbourhood's advertised count back in step with what a member can actually find there.
/// <para>
/// <b>It was recomputed on publication and on a letting, and nowhere else.</b> So a property that
/// was archived, taken off the market, hidden by moderation, made private or deleted stayed in its
/// neighbourhood's number until somebody in that neighbourhood published something. The occupied
/// handler already stated the reason the count must be fresh — a directory that still advertised a
/// let property would send members to a home somebody else is living in — and that argument applies
/// word for word to one that has been withdrawn.
/// </para>
/// <para>
/// <b>Every condition the count turns on, not only the status.</b> A property is counted when it is
/// public, clear of moderation, and Published, Available or Reserved. Three separate things can end
/// that: a lifecycle move, a moderation decision, and a landlord making the listing private. All
/// three reach this, because a count that followed only the first would be exactly as wrong as the
/// one it replaces, on two paths out of three.
/// </para>
/// <para>
/// <b>Taken from the listing rather than from the event.</b> The lifecycle events carry the
/// property and the landlord but not the area, and adding it to four event types would put the same
/// fact in four places that can drift. The lookup reads past the soft-delete filter for the one
/// case that needs it — see <c>NeighborhoodAreaOfAsync</c>, where the property is gone and its
/// neighbourhood most needs recounting.
/// </para>
/// <para>
/// <b>Nothing here may fail the move that triggered it.</b> A property in the right state beside a
/// stale count is a cosmetic problem; a moderation decision rolled back because a counter would not
/// update is not. An unfiled property and an unknown neighbourhood are therefore both returns.
/// </para>
/// </summary>
public sealed class NeighborhoodListingCountRefresher
{
    private readonly IHousingListingRepository _listings;
    private readonly INeighborhoodProfileRepository _profiles;

    public NeighborhoodListingCountRefresher(
        IHousingListingRepository listings, INeighborhoodProfileRepository profiles)
    {
        _listings = listings;
        _profiles = profiles;
    }

    public async Task RefreshAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        var areaId = await _listings.NeighborhoodAreaOfAsync(listingId, cancellationToken);

        // Most properties are filed under an area by `AreaFilingSweep`, but not all of them are,
        // and one that is not belongs to no neighbourhood's count.
        if (areaId is not { } area || area == Guid.Empty)
        {
            return;
        }

        var profile = await _profiles.GetByAreaAsync(area, cancellationToken);

        if (profile is null)
        {
            return;
        }

        profile.SetListingCount(
            await _listings.CountInNeighborhoodAsync(area, cancellationToken));
    }
}
