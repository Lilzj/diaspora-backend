namespace Diaspora.Modules.Community.Domain.Enums;

/// <summary>
/// Which way a reaction points, for ranking.
/// <para>
/// A reaction catalogue that is only a list of labels cannot be ranked on: a ranking strategy
/// summing every reaction equally would treat a dispute as engagement and push a contested post
/// up the feed. Every reaction definition declares a sentiment, and the built-in ranking
/// strategies weight by it.
/// </para>
/// <para>
/// <see cref="Corroborating"/> is separate from <see cref="Positive"/> on purpose. A member
/// saying <em>I can confirm this</em> is making a factual claim, not expressing approval, and
/// it is the only reaction that feeds the community verification threshold.
/// </para>
/// </summary>
public enum ReactionSentiment
{
    Neutral = 0,

    Positive = 1,

    /// <summary>A claim that the content is accurate. Feeds the verification threshold.</summary>
    Corroborating = 2,

    /// <summary>A claim that the content is wrong. Feeds the disputed state.</summary>
    Disputing = 3,

    Negative = 4
}
