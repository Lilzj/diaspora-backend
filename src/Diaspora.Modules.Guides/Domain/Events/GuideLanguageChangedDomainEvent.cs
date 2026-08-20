using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a guide's language is corrected.</summary>
public sealed record GuideLanguageChangedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required string PreviousLanguage { get; init; }

    public required string CurrentLanguage { get; init; }
}
