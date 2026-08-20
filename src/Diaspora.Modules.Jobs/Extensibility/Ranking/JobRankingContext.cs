namespace Diaspora.Modules.Jobs.Extensibility.Ranking;

/// <summary>
/// What a ranking strategy is allowed to know about the person it is ranking for.
/// <para>
/// <b>Deliberately thin, and thinner than it could be.</b> A strategy could in principle be
/// handed the whole candidate profile — their salary expectation, their visa status, their work
/// history — and rank on all of it. It is not, because a ranking seam is the easiest place in a
/// job platform for personalisation to quietly become discrimination, and because a strategy
/// that saw a candidate's expected salary could rank down the jobs that pay more than it.
/// </para>
/// <para>
/// What a strategy gets is where the candidate is, whether they need sponsorship, and what
/// skills they claim — the three things that decide whether a posting is <em>reachable</em> for
/// them, rather than whether they are a good bet for it. Anything richer belongs in a
/// recommendation model, which is a separate seam and explicitly out of scope for this phase.
/// </para>
/// </summary>
public sealed record JobRankingContext
{
    /// <summary>Where the candidate is now. Upper-case ISO 3166-1 alpha-2.</summary>
    public string? ViewerCountryCode { get; init; }

    /// <summary>Countries the candidate said they would work in.</summary>
    public IReadOnlyList<string> PreferredCountries { get; init; } = [];

    /// <summary>
    /// Whether the candidate would need an employer to sponsor them where they are looking. A
    /// strategy may lift sponsoring postings for someone who needs one; it must never bury a
    /// posting because of it.
    /// </summary>
    public bool NeedsSponsorship { get; init; }

    /// <summary>Normalised skill keys the candidate claims.</summary>
    public IReadOnlyList<string> SkillKeys { get; init; } = [];

    /// <summary>The free-text term, when the surface was a search rather than a browse.</summary>
    public string? SearchTerm { get; init; }

    /// <summary>The instant the ranking is for. Recency decay measures against it.</summary>
    public required DateTimeOffset Now { get; init; }

    /// <summary>Nothing known about the viewer. An anonymous browse.</summary>
    public static JobRankingContext Anonymous(DateTimeOffset now) => new() { Now = now };
}
