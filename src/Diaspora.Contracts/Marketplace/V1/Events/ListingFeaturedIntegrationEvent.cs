using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// Featured placement was given or withdrawn.
/// <para>
/// One contract for both directions, with <see cref="IsFeatured"/> saying which. A consumer
/// maintaining a featured rail needs the withdrawal at least as much as the grant — a rail that
/// only ever gains entries ends up showing nothing but promotions that finished last month.
/// </para>
/// </summary>
public sealed record ListingFeaturedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public required bool IsFeatured { get; init; }

    /// <summary>The promotion behind it, when one drove the change.</summary>
    public Guid? PromotionId { get; init; }

    /// <summary><c>Featured</c>, <c>Bump</c>, <c>Spotlight</c>, <c>CategoryHighlight</c>.</summary>
    public string? PromotionKind { get; init; }

    /// <summary>When the placement lapses, so a rail can expire it without a second event.</summary>
    public DateTimeOffset? EndsAt { get; init; }

    public Guid? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
