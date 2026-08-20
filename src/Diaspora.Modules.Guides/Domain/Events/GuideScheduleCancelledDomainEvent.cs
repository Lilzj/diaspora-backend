using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a scheduled publication is called off before it fires.</summary>
public sealed record GuideScheduleCancelledDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }
}
