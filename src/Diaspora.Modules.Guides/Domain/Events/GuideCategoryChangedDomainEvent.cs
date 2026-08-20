using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a guide moves to another category. Category pages re-count on this.</summary>
public sealed record GuideCategoryChangedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required string PreviousCategoryKey { get; init; }

    public required string CurrentCategoryKey { get; init; }
}
