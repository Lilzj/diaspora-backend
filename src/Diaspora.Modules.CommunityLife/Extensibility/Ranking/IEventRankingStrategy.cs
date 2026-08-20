namespace Diaspora.Modules.CommunityLife.Extensibility.Ranking;

/// <summary>
/// How an event is scored for a relevance-ordered page.
/// <para>
/// A seam rather than a hard-coded formula, because what makes an event worth showing first is a
/// product judgement that will change: today it is imminence and popularity, tomorrow it may be
/// whether anybody in your neighbourhood is going. A strategy is a class in whichever assembly owns
/// the idea, and this module gains no line when one is added.
/// </para>
/// <para>
/// <b>Scores are combined by weighted sum across every registered strategy</b>, so two strategies
/// compose rather than compete. A strategy that wants to be decisive returns a large weight; one
/// that wants to nudge returns a small one.
/// </para>
/// </summary>
public interface IEventRankingStrategy
{
    /// <summary>What this strategy is, for diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// How much this strategy counts, relative to the others. One is the default; a strategy
    /// returning zero is registered and inert, which is a useful state for one being trialled.
    /// </summary>
    double Weight => 1.0;

    /// <summary>
    /// The score, nought to one. Values outside that range are clamped rather than rejected,
    /// because a strategy in somebody else's assembly returning 1.5 should not take down a browse
    /// screen.
    /// </summary>
    double Score(EventRankingContext context);
}
