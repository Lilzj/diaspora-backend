using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a draft's publication is scheduled for a future instant.</summary>
public sealed record GuideScheduledDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required DateTimeOffset ScheduledFor { get; init; }
}
