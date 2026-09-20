using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;

namespace Diaspora.Modules.Housing.Application.Services;

/// <summary>
/// Puts a landlord's listing counts back in step with their listings.
/// <para>
/// <b>Publication was the only moment they were ever recomputed.</b> A landlord who archived a
/// property, let it, made it unavailable or deleted it kept the figure they had at their last
/// publication — and only publishing something else corrected it. The counts could therefore only
/// ever be too high, which is the direction that flatters.
/// </para>
/// <para>
/// <b>Recomputed, never adjusted.</b> The module already reasons this way about the neighbourhood
/// count, in B1.1's words: an increment applied twice on a redelivery drifts upward forever. Domain
/// events here are dispatched at least once, so a <c>+1</c> or <c>-1</c> would be a counter that
/// slowly parts company with the table it describes. Two indexed counts on a path that runs when a
/// landlord changes a property is not a cost worth that.
/// </para>
/// <para>
/// <b>Nothing here may fail the move that triggered it.</b> A property in the right state beside a
/// stale count is a cosmetic problem; an archive rolled back because a counter would not update
/// would lose the landlord's decision. A landlord that cannot be found is therefore a return, not
/// a throw — the profile may have been removed in the same breath as its listings.
/// </para>
/// </summary>
public sealed class LandlordListingCountRefresher
{
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;

    public LandlordListingCountRefresher(
        IHousingListingRepository listings, ILandlordProfileRepository landlords)
    {
        _listings = listings;
        _landlords = landlords;
    }

    public async Task RefreshAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlordId);

        var landlord = await _landlords.GetAsync(landlordId, cancellationToken);

        if (landlord is null)
        {
            return;
        }

        // **On the market, not "against the quota".** The figure used to come from
        // `CountAgainstListingQuotaAsync`, which counts drafts, occupied and unavailable properties
        // — correctly, because the quota exists to stop a script creating drafts. Read as a
        // landlord's advertisements it counted their private drafts and their let homes, so it was
        // wrong even on the day it was written.
        var onMarket = await _listings.CountOnMarketByLandlordAsync(landlordId, cancellationToken);

        // The real total, rather than `Math.Max(onMarket, whatever was there)`. That expression
        // could only ever ratchet upward, and it never once counted anything.
        var total = await _listings.CountByLandlordAsync(landlordId, cancellationToken);

        landlord.RecordListingCounts(onMarket, total);
    }
}
