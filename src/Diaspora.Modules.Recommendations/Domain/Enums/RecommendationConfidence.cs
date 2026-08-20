namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// How much the platform believes its own recommendation.
/// <para>
/// <b>Deterministic, and derived from evidence rather than from the score.</b> A candidate can score
/// highly on a thin profile — one location signal and nothing else will happily rank the most
/// popular guide in the country first — and presenting that with the same confidence as a match
/// built from forty signals would be dishonest in exactly the place the module is asking to be
/// trusted.
/// </para>
/// <para>
/// Confidence is a function of how many distinct signal kinds contributed and how many strategies
/// agreed, never of the score itself. The brief calls for it explicitly and calls it deterministic;
/// this is what deterministic means here.
/// </para>
/// </summary>
public enum RecommendationConfidence
{
    /// <summary>Cold start, or a single weak strategy. Honest about knowing nothing.</summary>
    Low = 0,

    /// <summary>Some corroboration: two or more strategies, or a profile with real signal behind it.</summary>
    Medium = 1,

    /// <summary>Several strategies agreed on a profile with breadth. The rarest of the three.</summary>
    High = 2
}
