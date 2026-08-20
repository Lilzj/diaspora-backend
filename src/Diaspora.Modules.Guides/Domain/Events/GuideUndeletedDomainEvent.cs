using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a soft delete is undone.</summary>
public sealed record GuideUndeletedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }
}
