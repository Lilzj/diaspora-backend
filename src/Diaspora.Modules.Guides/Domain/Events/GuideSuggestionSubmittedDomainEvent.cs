using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>Raised when a member proposes an improvement. The author's queue listens here.</summary>
public sealed record GuideSuggestionSubmittedDomainEvent : DomainEvent
{
    public required GuideSuggestionId SuggestionId { get; init; }

    public required GuideId GuideId { get; init; }

    public required UserId AuthorId { get; init; }
}
