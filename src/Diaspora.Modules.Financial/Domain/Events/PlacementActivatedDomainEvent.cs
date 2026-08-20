using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A paid placement is now in force over somebody else's subject.
/// <para>
/// <b>One contract for six placement kinds across ten subject types, rather than sixty.</b> The
/// brief names <c>PremiumListingActivated</c>, <c>FeaturedProviderActivated</c> and
/// <c>BoostPurchased</c> as examples; each is this event with a particular
/// <see cref="Kind"/> and <see cref="SubjectType"/>. Marketplace has published featuring this way
/// since B10 — one contract, a discriminator, and both directions — and a consumer switching on a
/// discriminator is a great deal less fragile than one subscribing to the right sixtieth of a
/// contract family.
/// </para>
/// </summary>
public sealed record PlacementActivatedDomainEvent : DomainEvent
{
    public required PlacementId PlacementId { get; init; }

    public required MonetizationSubjectType SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required PlacementKind Kind { get; init; }

    public required Guid PurchasedByUserId { get; init; }

    /// <summary>What a ranking consumer should weight this by. 1.0 is no change.</summary>
    public required decimal PriorityWeight { get; init; }

    public required string ProductCode { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    /// <summary>When it lapses, so a rail can expire it without waiting for a second event.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    public required DateTimeOffset ActivatedAt { get; init; }
}
