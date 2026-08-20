using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A listing became visible to buyers.
/// <para>
/// <b>The most consequential contract this module publishes.</b> It is what puts an item into a
/// search index, fires every matching standing search, adds a row to the neighbourhood's nearby
/// panel and starts the clock the seller's response reputation is measured against.
/// </para>
/// <para>
/// <see cref="IsFirstPublication"/> is how a consumer tells "a new item exists" from "an existing
/// item was corrected": a notification fan-out wants the first only — a buyer re-notified because
/// the seller fixed a typo learns to ignore notifications — while a search index wants them all.
/// </para>
/// <para>
/// The payload carries everything a card-level index needs, so most consumers never call back into
/// the Marketplace module. <b><see cref="PriceAmount"/> travels with its currency and is never
/// converted:</b> a rate has a date, and a price silently restated is a number a buyer would act
/// on and be wrong about.
/// </para>
/// </summary>
public sealed record ListingPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    /// <summary>The seller's trading name, denormalised so a card needs no second read.</summary>
    public required string SellerName { get; init; }

    /// <summary><c>Individual</c>, <c>VerifiedSeller</c>, <c>Business</c>, <c>VerifiedBusiness</c>,
    /// <c>ProviderStore</c>. Computed at publication; a consumer that caches it should refresh on
    /// <c>SellerVerified</c>.</summary>
    public required string SellerType { get; init; }

    public required bool IsSellerVerified { get; init; }

    public required string Title { get; init; }

    /// <summary>The deep-link handle. Stable across title edits.</summary>
    public required string Slug { get; init; }

    public string? Summary { get; init; }

    public required string CategoryKey { get; init; }

    /// <summary>Normalised tag slugs, in display order.</summary>
    public required IReadOnlyList<string> Tags { get; init; }

    /// <summary><c>FixedPrice</c>, <c>Negotiable</c>, <c>Wanted</c>, <c>Giveaway</c>,
    /// <c>BusinessInventory</c>.</summary>
    public required string ListingType { get; init; }

    /// <summary><c>New</c>, <c>LikeNew</c>, <c>Excellent</c>, <c>Good</c>, <c>Fair</c>,
    /// <c>ForParts</c>, <c>NotApplicable</c>.</summary>
    public required string Condition { get; init; }

    /// <summary>False when the seller published no figure — a wanted advert with no budget.</summary>
    public required bool IsPriceDisclosed { get; init; }

    public decimal? PriceAmount { get; init; }

    /// <summary>ISO 4217, upper case. Never converted.</summary>
    public string? CurrencyCode { get; init; }

    public required bool IsNegotiable { get; init; }

    /// <summary>Units available at publication.</summary>
    public required int Quantity { get; init; }

    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public required bool OffersPickup { get; init; }

    public required bool OffersDelivery { get; init; }

    public required bool OffersShipping { get; init; }

    /// <summary>The card image, by reference. Media owns the bytes and the URL.</summary>
    public Guid? CoverMediaId { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>False on a republication. Standing searches fire on the first only.</summary>
    public required bool IsFirstPublication { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// The member behind the seller profile — B23.x Wave 12.
    /// <para>
    /// <b>The field a seller-scoped rule cannot be matched without.</b> An automation owned by a
    /// seller is owned by the <em>member</em>, and the automation platform's own scope authority
    /// answers "is this your scope" by comparing the owner identifier with the caller's user id. This
    /// contract named only <see cref="SellerId"/> — the profile — so a consumer had a listing's owner
    /// and no way to reach the person, and "one of my listings is about to expire" could not be
    /// matched to anybody. <c>OfferCreatedIntegrationEvent</c> has carried both since B10 for exactly
    /// this reason and states it in the same words.
    /// </para>
    /// <para>
    /// <b>Costs nothing to publish.</b> The relay already reads the seller aggregate back to fill in
    /// <see cref="SellerName"/> and <see cref="IsSellerVerified"/>, so there is no new read.
    /// </para>
    /// <para>
    /// <b>Appended, never inserted.</b> Property order is part of the serialised shape, and a field
    /// added in the middle changes how every previously-written outbox row deserialises. Null on
    /// events published before Wave 12, and null when the seller could not be read.
    /// </para>
    /// </summary>
    public Guid? SellerUserId { get; init; }
}
