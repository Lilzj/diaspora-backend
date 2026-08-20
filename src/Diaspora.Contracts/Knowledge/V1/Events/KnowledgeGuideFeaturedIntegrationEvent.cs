using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Knowledge.V1.Events;

/// <summary>
/// An editor featured a guide on one country's directory.
/// <para>
/// <b>Not the same fact as the guide featuring event B8 publishes, and the difference is
/// scope.</b> The B8 flag is global: a featured guide is featured everywhere, which is the right
/// shape for a platform-wide carousel and the wrong one for a knowledge directory, where the first
/// thing to read about Mauritius and the first thing to read about Canada are different guides and
/// a single boolean cannot hold both. This event carries the country and, optionally, the category
/// the guide was featured within.
/// </para>
/// <para>
/// Split from its unfeatured counterpart rather than carrying a boolean, because the payloads
/// genuinely differ: featuring places a guide at a position within a category, and unfeaturing
/// removes it from wherever it was.
/// </para>
/// </summary>
public sealed record KnowledgeGuideFeaturedIntegrationEvent : IntegrationEvent
{
    public required Guid GuideId { get; init; }

    /// <summary>Upper-case ISO 3166-1 alpha-2 — the directory the guide was featured on.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The knowledge category it was featured within, or null for the country shelf.</summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>Where it sits among the featured guides of the country. Lower shows first.</summary>
    public required int Position { get; init; }

    /// <summary>The one-line reason from the editor. Rendered as a curator note, never interpreted.</summary>
    public string? Note { get; init; }

    public required Guid ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
