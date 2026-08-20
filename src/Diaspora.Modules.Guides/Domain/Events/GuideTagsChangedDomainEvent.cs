using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when the tag set is replaced. Carries the new set, for re-indexing.</summary>
public sealed record GuideTagsChangedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }
}
