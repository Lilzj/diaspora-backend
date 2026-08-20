using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when a guide starts recommending a provider. A referral signal B7's analytics can
/// consume without B8 knowing it does.
/// </summary>
public sealed record GuideProviderReferencedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required Guid ProviderId { get; init; }

    public string? Role { get; init; }
}
