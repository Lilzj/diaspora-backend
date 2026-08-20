using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a guide is soft-deleted. Consumers de-index; the row survives for audit.</summary>
public sealed record GuideDeletedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
