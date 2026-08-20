using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A curator featured a guide on a country directory, or removed it from one.
/// <para>
/// <b>Country-scoped featuring, which is the whole point of it.</b> The guide aggregate already
/// carries a global featured flag that B8 owns, and it answers a different question: "should this
/// appear in the platform carousel". This answers "is this one of the first things somebody moving
/// to Mauritius should read", and the same guide can be the answer for one country and irrelevant
/// to the next.
/// </para>
/// <para>
/// Raised by the directory aggregate, never by the guide, so featuring a guide does not load, lock
/// or bump the revision of content B8 owns.
/// </para>
/// </summary>
public sealed record CountryGuideFeatureChangedDomainEvent : DomainEvent
{
    public required CountryKnowledgeDirectoryId DirectoryId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required GuideId GuideId { get; init; }

    /// <summary>True when it became featured, false when the placement was removed.</summary>
    public required bool Featured { get; init; }

    /// <summary>The category it was featured within, or null for the country shelf.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>Where it sits among the featured guides. Zero when unfeaturing.</summary>
    public required int Position { get; init; }

    /// <summary>The curator note. Null when unfeaturing, and when the curator wrote none.</summary>
    public string? Note { get; init; }

    public required UserId ChangedBy { get; init; }
}
