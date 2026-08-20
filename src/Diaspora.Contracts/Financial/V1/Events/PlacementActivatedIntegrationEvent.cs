using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A paid placement is now in force over a subject.
/// <para>
/// <b>One contract for six placement kinds across ten subject types, rather than sixty.</b> B15's
/// brief names <c>PremiumListingActivated</c>, <c>FeaturedProviderActivated</c> and
/// <c>BoostPurchased</c> as examples; each is this event with a particular <see cref="Kind"/> and
/// <see cref="SubjectType"/>. Marketplace has published featuring this way since B10 — one contract,
/// a discriminator, and both directions — and a consumer switching on a discriminator is a great deal
/// less fragile than one subscribing to the right sixtieth of a contract family.
/// </para>
/// <para>
/// <b>This is <em>purchased</em> placement and it is not the same thing as a module's own
/// <c>IsFeatured</c> flag.</b> That flag is a moderator's editorial decision, free, and stays where
/// it is. A consumer that wants to know "did this rank because somebody paid" reads this contract and
/// nothing else.
/// </para>
/// <para>
/// <see cref="PriorityWeight"/> is what a ranking consumer should weight the subject by; 1.0 is no
/// change. The Financial Platform publishes the number and deliberately never learns what B14 did
/// with it.
/// </para>
/// </summary>
public sealed record PlacementActivatedIntegrationEvent : IntegrationEvent
{
    public required Guid PlacementId { get; init; }

    /// <summary>
    /// <c>MarketplaceListing</c>, <c>HousingListing</c>, <c>Job</c>, <c>Provider</c>,
    /// <c>Organization</c>, <c>Event</c>, <c>Guide</c>, <c>Update</c>, <c>Community</c>,
    /// <c>Member</c>.
    /// </summary>
    public required string SubjectType { get; init; }

    /// <summary>The identifier in the module that owns the subject.</summary>
    public required Guid SubjectId { get; init; }

    /// <summary>
    /// <c>Featured</c>, <c>Bump</c>, <c>Spotlight</c>, <c>CategoryHighlight</c>, <c>Boost</c> or
    /// <c>Premium</c>. The first four are Marketplace's own vocabulary, unchanged.
    /// </summary>
    public required string Kind { get; init; }

    public required Guid PurchasedByUserId { get; init; }

    public required decimal PriorityWeight { get; init; }

    public required string ProductCode { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    /// <summary>When it lapses, so a rail can expire it without waiting for a second event.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    public required DateTimeOffset ActivatedAt { get; init; }
}
