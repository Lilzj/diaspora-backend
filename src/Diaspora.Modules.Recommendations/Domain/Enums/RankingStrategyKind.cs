namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// The family a ranking strategy belongs to.
/// <para>
/// <b>The kind is what the platform knows about a strategy; the key is what identifies it.</b> A
/// deployment may configure three location strategies with different radii and different weights on
/// three surfaces, and all three are <see cref="Location"/>. Nothing branches on the kind at ranking
/// time — the registry resolves by key — but the analytics rollup groups by it, and the catalogue
/// refuses a configured strategy whose kind has no registered implementation, which is how a
/// mistyped key fails at startup instead of silently contributing zero to every score.
/// </para>
/// <para>
/// <b><see cref="MachineLearning"/>, <see cref="VectorSimilarity"/> and <see cref="LanguageModel"/>
/// are declared and unimplemented, on purpose.</b> They are the AI seam made concrete: a future
/// adapter registers an <c>IRankingStrategy</c> whose kind is one of these, the catalogue accepts
/// it, and every surface can weight it against the deterministic strategies without one line of
/// this module changing. Until then the registry reports them as unavailable and the catalogue
/// refuses to enable one, which is a startup failure rather than a silently unranked surface.
/// </para>
/// </summary>
public enum RankingStrategyKind
{
    /// <summary>How much attention a candidate is getting, normalised within its target type.</summary>
    Popularity = 0,

    /// <summary>How recently a candidate was published or updated.</summary>
    Recency = 1,

    /// <summary>How close a candidate is to where the member actually is.</summary>
    Location = 2,

    /// <summary>How verified the candidate and its author are.</summary>
    Trust = 3,

    /// <summary>How much a candidate resembles what the member has already engaged with.</summary>
    Similarity = 4,

    /// <summary>How well a candidate matches the member's stated and derived interests.</summary>
    InterestMatch = 5,

    /// <summary>A flat weight applied from a configured rule. The manual override.</summary>
    RuleWeighting = 6,

    /// <summary>Demotes a candidate that resembles one already higher on the page.</summary>
    Diversity = 7,

    /// <summary>What to show somebody the platform knows nothing about yet.</summary>
    ColdStart = 8,

    /// <summary>What to show when every other strategy returned nothing.</summary>
    Fallback = 9,

    /// <summary>Reserved for a future adapter. No implementation ships. See the class summary.</summary>
    MachineLearning = 100,

    /// <summary>Reserved for a future adapter. No implementation ships. See the class summary.</summary>
    VectorSimilarity = 101,

    /// <summary>Reserved for a future adapter. No implementation ships. See the class summary.</summary>
    LanguageModel = 102
}
