using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised when a guide is created, as a draft nobody but the author can see.
/// <para>
/// Separate from <see cref="GuidePublishedDomainEvent"/> on purpose: a creation is worth
/// auditing and counting, and emphatically not worth indexing or announcing — a draft is not
/// knowledge yet. Downstream consumers index on publication.
/// </para>
/// </summary>
public sealed record GuideCreatedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required UserId AuthorId { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    public required string CategoryKey { get; init; }

    public required string Language { get; init; }

    public required string CountryCode { get; init; }
}
