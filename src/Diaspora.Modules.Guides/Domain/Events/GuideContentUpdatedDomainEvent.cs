using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when the editor replaces the guide's structure. The draft changed; the published
/// version did not — consumers re-indexing published content wait for
/// <see cref="GuidePublishedDomainEvent"/>.
/// </summary>
public sealed record GuideContentUpdatedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public UserId? ChangedBy { get; init; }

    public required int SectionCount { get; init; }

    public required int BlockCount { get; init; }
}
