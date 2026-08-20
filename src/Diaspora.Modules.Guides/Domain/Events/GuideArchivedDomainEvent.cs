using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when a published guide is retired. Consumers de-index; the versions stay, because
/// history is permanent even when the guide is not current.
/// </summary>
public sealed record GuideArchivedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }
}
