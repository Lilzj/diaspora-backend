using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a media asset is detached and the claim can be released.</summary>
public sealed record GuideMediaDetachedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required Guid MediaId { get; init; }

    public required GuideMediaRole Role { get; init; }
}
