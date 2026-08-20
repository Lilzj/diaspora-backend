using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when a media asset is attached. The claim signal — the handler that tells Media the
/// asset is in use listens here, so Media's sweeper does not collect it.
/// </summary>
public sealed record GuideMediaAttachedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required Guid MediaId { get; init; }

    public required GuideMediaRole Role { get; init; }
}
