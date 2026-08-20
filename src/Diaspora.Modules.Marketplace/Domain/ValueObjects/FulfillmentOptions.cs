using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// How the buyer gets the item.
/// <para>
/// <b>Booleans rather than a flags enum, and the reason is the database.</b> A flags enum stored
/// as an integer makes "listings that deliver" a bitwise predicate no index can serve; four
/// boolean columns make it an indexed equality. The brief's "pickup options, delivery options,
/// shipping availability" are three of them.
/// </para>
/// <para>
/// <b>Nothing here tracks a shipment.</b> The brief rules that out, and the boundary is worth
/// stating: this records what the seller says they will do, so a buyer can filter on it. A
/// courier integration is a future module that reads this and owns everything after it.
/// </para>
/// <para>
/// <b>At least one option must be true, and that check lives in the aggregate rather than here.</b>
/// A draft is allowed to be incomplete — the seller has not finished — and publication is where
/// the listing has to be able to answer "how do I get it".
/// </para>
/// </summary>
public sealed class FulfillmentOptions : ValueObject
{
    private FulfillmentOptions(
        bool offersPickup,
        bool offersDelivery,
        bool offersShipping,
        bool isDigital,
        int? deliveryRadiusKm,
        string? notes)
    {
        OffersPickup = offersPickup;
        OffersDelivery = offersDelivery;
        OffersShipping = offersShipping;
        IsDigital = isDigital;
        DeliveryRadiusKm = deliveryRadiusKm;
        Notes = notes;
    }

    private FulfillmentOptions() { }

    /// <summary>The buyer collects. The default on a peer-to-peer listing.</summary>
    public bool OffersPickup { get; private set; }

    /// <summary>The seller brings it, locally.</summary>
    public bool OffersDelivery { get; private set; }

    /// <summary>The seller posts it.</summary>
    public bool OffersShipping { get; private set; }

    /// <summary>
    /// Nothing physical changes hands. Future-ready for digital goods, and already correct for
    /// a service listing.
    /// </summary>
    public bool IsDigital { get; private set; }

    /// <summary>How far the seller will deliver. Meaningful only with delivery on.</summary>
    public int? DeliveryRadiusKm { get; private set; }

    /// <summary>Free text: "weekends only", "collection from the shop", "postage at cost".</summary>
    public string? Notes { get; private set; }

    /// <summary>Collection only. What a second-hand listing starts as.</summary>
    public static FulfillmentOptions PickupOnly =>
        new(true, false, false, false, null, null);

    /// <summary>Nothing chosen yet. What a fresh draft carries.</summary>
    public static FulfillmentOptions None =>
        new(false, false, false, false, null, null);

    /// <summary>Whether the seller has said anything at all about getting the item to a buyer.</summary>
    public bool IsSpecified =>
        OffersPickup || OffersDelivery || OffersShipping || IsDigital;

    public static Result<FulfillmentOptions> Create(
        bool offersPickup,
        bool offersDelivery,
        bool offersShipping,
        bool isDigital = false,
        int? deliveryRadiusKm = null,
        string? notes = null)
    {
        if (deliveryRadiusKm is { } radius
            && (radius <= 0 || radius > MarketplaceLimits.MaxDeliveryRadiusKm))
        {
            return Result.Failure<FulfillmentOptions>(MarketplaceErrors.FulfillmentInvalid);
        }

        var trimmed = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        if (trimmed is { Length: > MarketplaceLimits.MaxNoteLength })
        {
            return Result.Failure<FulfillmentOptions>(MarketplaceErrors.FulfillmentInvalid);
        }

        // A digital good has nothing to collect or post, and saying it does would put a
        // meaningless "collection from" line on the card.
        if (isDigital && (offersPickup || offersDelivery || offersShipping))
        {
            return Result.Failure<FulfillmentOptions>(MarketplaceErrors.FulfillmentInvalid);
        }

        return Result.Success(new FulfillmentOptions(
            offersPickup,
            offersDelivery,
            offersShipping,
            isDigital,

            // A radius without delivery is a number nobody will ever read.
            offersDelivery ? deliveryRadiusKm : null,
            trimmed));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return OffersPickup;
        yield return OffersDelivery;
        yield return OffersShipping;
        yield return IsDigital;
        yield return DeliveryRadiusKm;
        yield return Notes;
    }
}
