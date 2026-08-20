using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when featured placement is withdrawn.</summary>
public sealed record GuideUnfeaturedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }
}
