using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a member reviews a provider.
/// <para>
/// Consumed inside B7 to recompute <c>ProviderReviewSummary</c>, and published outward so B16
/// can tell the owner — which is the client's existing "New review from Amara K." notification,
/// the first item in its own mock notification list.
/// </para>
/// </summary>
public sealed record ProviderReviewSubmittedDomainEvent : DomainEvent
{
    public required ProviderReviewId ReviewId { get; init; }

    public required ProviderId ProviderId { get; init; }

    public required UserId ReviewerId { get; init; }

    public required int Rating { get; init; }

    public required bool HasBody { get; init; }
}
