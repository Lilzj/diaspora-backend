using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>Raised when a member recommends, endorses or writes a testimonial for a provider.</summary>
public sealed record ProviderRecommendationReceivedDomainEvent : DomainEvent
{
    public required ProviderRecommendationId RecommendationId { get; init; }

    public required ProviderId ProviderId { get; init; }

    public required UserId RecommenderId { get; init; }

    public required RecommendationKind Kind { get; init; }
}
