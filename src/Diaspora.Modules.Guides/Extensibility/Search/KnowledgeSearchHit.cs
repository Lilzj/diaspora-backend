namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// One result: which guide, and how well it matched.
/// <para>
/// <b>An identifier and a score, and nothing else.</b> An engine that returned titles would be
/// returning its own copy of them — and an external index is precisely the place where that copy is
/// most likely to be a day old. The caller resolves identifiers through B8, which is the only thing
/// that knows what a guide currently says.
/// </para>
/// </summary>
public sealed record KnowledgeSearchHit
{
    public required Guid GuideId { get; init; }

    /// <summary>
    /// The engine's relevance score. Meaningful only relative to other hits in the same answer —
    /// the shipped engine ranks rather than scores, and reports zero.
    /// </summary>
    public double Score { get; init; }
}
