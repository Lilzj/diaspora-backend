namespace Diaspora.Modules.Guides.Extensibility.Search;

/// <summary>
/// One result: which notice, and how well it matched.
/// <para>
/// <b>An identifier and a score, and nothing else.</b> An engine that returned titles would be
/// returning its own copy of them, and on official information a stale copy is worse than none — an
/// index a day behind would show the headline of a notice that was superseded this morning. The
/// caller resolves identifiers against the write model, which is the only thing that knows what a
/// notice currently says and whether it still stands.
/// </para>
/// </summary>
public sealed record NoticeSearchHit
{
    public required Guid OfficialNoticeId { get; init; }

    /// <summary>
    /// The engine's relevance score. Meaningful only relative to other hits in the same answer — the
    /// shipped engine ranks rather than scores, and reports zero.
    /// </summary>
    public double Score { get; init; }
}
