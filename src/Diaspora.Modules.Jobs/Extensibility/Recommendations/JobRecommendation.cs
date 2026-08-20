namespace Diaspora.Modules.Jobs.Extensibility.Recommendations;

/// <summary>
/// One suggested job, with the reason it was suggested.
/// <para>
/// <b><see cref="Reason"/> is required, and that is the point of the type.</b> A recommendation
/// a candidate cannot account for is one they cannot trust or correct — and on a platform where
/// the suggestion may be "move country for this", an unexplained ranking is not good enough.
/// Requiring the reason now, while the implementation is a heuristic that can trivially produce
/// one, is what stops a later model shipping without it.
/// </para>
/// </summary>
public sealed record JobRecommendation
{
    public required Guid JobId { get; init; }

    /// <summary>Comparable only within one result set.</summary>
    public double Score { get; init; }

    /// <summary>
    /// Why, in words a candidate reads: "matches 4 of your skills", "sponsors visas for
    /// Germany", "in a country you said you'd move to".
    /// </summary>
    public required string Reason { get; init; }
}
