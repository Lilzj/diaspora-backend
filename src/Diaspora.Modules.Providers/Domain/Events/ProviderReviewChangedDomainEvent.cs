using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a review's rating or status moved after it was first written.
/// <para>
/// Exists because <c>ProviderReviewSummary</c> is a rollup, and a rollup that only listens for
/// new reviews drifts the first time one is edited from four stars to two, hidden by a
/// moderator, or withdrawn by its author. That drift is silent — nothing compares the summary
/// to the truth — and a rating nobody can reconcile is a rating nobody should trust.
/// </para>
/// </summary>
public sealed record ProviderReviewChangedDomainEvent : DomainEvent
{
    public required ProviderReviewId ReviewId { get; init; }

    public required ProviderId ProviderId { get; init; }

    public required int PreviousRating { get; init; }

    public required int CurrentRating { get; init; }

    public required ReviewStatus PreviousStatus { get; init; }

    public required ReviewStatus CurrentStatus { get; init; }
}
