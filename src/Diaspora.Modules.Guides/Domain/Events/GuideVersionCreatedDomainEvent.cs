using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised alongside <see cref="GuidePublishedDomainEvent"/> to say a new immutable version
/// number exists. The handler that writes the <c>GuideVersion</c> snapshot listens here.
/// </summary>
public sealed record GuideVersionCreatedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required int Version { get; init; }

    public required UserId EditorId { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}
