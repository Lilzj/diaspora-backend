namespace Diaspora.Modules.Jobs.Extensibility.Search;

/// <summary>
/// One result: an identifier, a score, and optionally why.
/// <para>
/// <b>No content.</b> The read path hydrates the card from the write model, so a stale index can
/// misrank a result but can never misrender one — and can never surface a posting whose
/// visibility changed after it was indexed.
/// </para>
/// </summary>
public sealed record JobSearchHit
{
    public required Guid JobId { get; init; }

    /// <summary>
    /// The engine's own score. Comparable only within one result set — engines differ, and
    /// nothing in this module persists or compares scores across searches.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Which fields matched, when the engine can say cheaply. Renders the "matched: nursing,
    /// visa sponsorship" hint on a card; an engine that cannot supply it returns nothing and the
    /// card simply omits the hint.
    /// </summary>
    public IReadOnlyList<string>? MatchedFields { get; init; }
}
