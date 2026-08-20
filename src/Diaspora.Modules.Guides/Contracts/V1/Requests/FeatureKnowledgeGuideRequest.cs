namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// Puts a guide on one country's curated shelf.
/// <para>
/// <b>Country-scoped, which is what the global featured flag on a guide cannot express.</b> The
/// same guide is legitimately the first thing to read about one country and irrelevant to the next,
/// and this does not touch the guide at all — B8 owns the guide, and featuring it here neither
/// loads it for writing nor bumps its revision.
/// </para>
/// </summary>
public sealed record FeatureKnowledgeGuideRequest
{
    public required Guid GuideId { get; init; }

    /// <summary>
    /// The navigation row to feature it within, or null for the country's own shelf.
    /// <para>
    /// Both are useful and they are different shelves: the country shelf is what somebody sees on
    /// arrival, and a category shelf is what somebody sees having already decided they have a
    /// housing question.
    /// </para>
    /// </summary>
    public string? KnowledgeCategoryKey { get; init; }

    /// <summary>Where on the shelf. Null appends; a value inserts and renumbers.</summary>
    public int? Position { get; init; }

    /// <summary>The curator's one-line reason. Rendered verbatim, never interpreted.</summary>
    public string? Note { get; init; }

    /// <summary>The revision the client was looking at.</summary>
    public int? Revision { get; init; }
}
