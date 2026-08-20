using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// Raised every time a guide is published, first time or edit. The event every downstream
/// consumer actually wants: this is when a guide enters search, becomes recommendable and is
/// worth telling a community about. <see cref="IsFirstPublication"/> distinguishes news from a
/// correction.
/// </summary>
public sealed record GuidePublishedDomainEvent : DomainEvent
{
    public required GuideId GuideId { get; init; }

    public required UserId AuthorId { get; init; }

    public required string Slug { get; init; }

    public required string Title { get; init; }

    public required string CategoryKey { get; init; }

    public required string CountryCode { get; init; }

    public required string Language { get; init; }

    public required int Version { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public required bool IsFirstPublication { get; init; }
}
