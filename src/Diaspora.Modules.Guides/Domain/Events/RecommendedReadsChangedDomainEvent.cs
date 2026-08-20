using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// A curator changed what to read after a guide.
/// <para>
/// <b>No integration contract is published for this, deliberately.</b> Recommended reads are the
/// finest-grained editorial act in the module — a curator adjusting one guide's follow-ons — and
/// publishing each one would put a message on the bus for a change no module outside B8.1 renders
/// independently. The event exists so the read cache for that guide is dropped, which is the only
/// consequence there is.
/// </para>
/// </summary>
public sealed record RecommendedReadsChangedDomainEvent : DomainEvent
{
    public required KnowledgeRecommendationId RecommendationId { get; init; }

    /// <summary>The guide somebody just finished reading.</summary>
    public required GuideId SourceGuideId { get; init; }

    /// <summary>How many next reads are curated after the change. Zero is a valid answer.</summary>
    public required int RecommendedCount { get; init; }

    public UserId? ChangedBy { get; init; }
}
