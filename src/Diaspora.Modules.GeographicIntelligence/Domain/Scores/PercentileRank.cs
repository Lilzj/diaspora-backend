using System.Globalization;

namespace Diaspora.Modules.GeographicIntelligence.Domain.Scores;

/// <summary>
/// Ranks one place's count against the other places it is comparable with.
/// <para>
/// <b>Why a rank and not a normalised count.</b> "This neighbourhood has 14 homes for rent" is a
/// real figure the platform already publishes. Turning it into a 0–100 score needs a ceiling, and
/// every ceiling that is not measured is invented — pick 50 and a busy district pins at 100, pick
/// 500 and every real neighbourhood reads as empty. A percentile needs no ceiling: it is a
/// statement about a population that exists, it is reproducible, and its methodology sentence can
/// name the population and its size.
/// </para>
/// <para>
/// <b>The floor is the honesty rule.</b> A percentile over three neighbourhoods says almost
/// nothing and reads as authoritative, so below a configured population this returns null and the
/// dimension is withheld with <see cref="DimensionUnavailableReason.InsufficientPopulation"/>
/// rather than published. This is the same instinct as the heat layer's minimum cell count applied
/// to a statistic instead of to a disclosure.
/// </para>
/// </summary>
public static class PercentileRank
{
    /// <summary>
    /// Where <paramref name="value"/> sits in <paramref name="population"/>, as 0–100, or null
    /// when the population is too small to rank against.
    /// <para>
    /// Uses the <em>midpoint</em> definition — the share strictly below plus half the share equal
    /// — so that every member of a population where all values tie scores 50 rather than 100. Ties
    /// are the ordinary case here: most neighbourhoods have zero of most things, and a rule that
    /// scored all of them 100th percentile would rank an empty place top.
    /// </para>
    /// </summary>
    public static int? Of(int value, IReadOnlyCollection<int> population, int minimumPopulation)
    {
        ArgumentNullException.ThrowIfNull(population);

        if (population.Count < Math.Max(2, minimumPopulation))
        {
            return null;
        }

        var below = 0;
        var equal = 0;

        foreach (var other in population)
        {
            if (other < value)
            {
                below++;
            }
            else if (other == value)
            {
                equal++;
            }
        }

        var rank = (below + (equal / 2d)) / population.Count * 100d;

        return (int)Math.Round(Math.Clamp(rank, 0d, 100d), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The sentence published beside a ranked figure.
    /// <para>
    /// <b>It names the population exactly, including the part that is inconvenient.</b> The
    /// comparison set comes from B11's published directory, which returns the most active profiled
    /// neighbourhoods <em>that have at least one live housing listing</em>, capped at fifty. An
    /// earlier draft of this sentence said "neighbourhoods that have a published profile", which is
    /// a wider set than the one actually used — a small overstatement, and precisely the kind that
    /// makes a provenance field worse than none, because a reader would take it at face value.
    /// </para>
    /// </summary>
    public static string Methodology(string what, int populationSize, string within) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "Percentile rank of {0} among the {1} most active neighbourhoods in {2} that have a "
            + "published Diaspora profile and at least one live housing listing. 50 means typical "
            + "for that set, not average quality.",
            what,
            populationSize,
            within);

    /// <summary>The explanation a breakdown row renders under the bar.</summary>
    public static string Explanation(int value, string noun, int populationSize) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1} here, ranked against {2} profiled neighbourhoods.",
            value,
            noun,
            populationSize);
}
