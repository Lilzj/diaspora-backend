using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when an archived guide returns to publication.</summary>
public sealed record GuideRestoredDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required DateTimeOffset RestoredAt { get; init; }
}
