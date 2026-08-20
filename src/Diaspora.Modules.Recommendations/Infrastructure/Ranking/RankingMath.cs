namespace Diaspora.Modules.Recommendations.Infrastructure.Ranking;

/// <summary>
/// The arithmetic every shipped strategy shares.
/// <para>
/// <b>Declared once because a normalisation written twice is a normalisation that differs
/// somewhere.</b> Two strategies that scale differently produce contributions that cannot be
/// compared, and the whole weighted sum stops meaning anything — silently, because every individual
/// number still looks reasonable.
/// </para>
/// </summary>
internal static class RankingMath
{
    /// <summary>
    /// Scales a value against the largest in its own pool.
    /// <para>
    /// <b>Relative rather than absolute, and that is the decision that lets the platform grow.</b>
    /// "A hundred views is popular" is true in a country with two thousand members and meaningless in
    /// one with two million; a constant threshold would have to be retuned per country per vertical
    /// forever. Scaling within the pool means the most popular thing available is always 1 and
    /// everything else is a fraction of it.
    /// </para>
    /// </summary>
    public static decimal Normalize(decimal value, decimal maximum)
    {
        if (maximum <= 0m)
        {
            return 0m;
        }

        return decimal.Round(Math.Clamp(value / maximum, 0m, 1m), 6);
    }

    /// <summary>
    /// How fresh something is, as an exponential decay.
    /// <para>
    /// A half-life rather than a window, for the reason <c>UserSignal.EffectiveWeight</c> records:
    /// a window makes yesterday's content and last week's content equally recent right up to a cliff
    /// nobody can see, and content published the day before the cliff falls off a surface overnight.
    /// </para>
    /// </summary>
    public static decimal Freshness(DateTimeOffset? timestamp, DateTimeOffset asOf, double halfLifeDays)
    {
        if (timestamp is null || halfLifeDays <= 0)
        {
            return 0m;
        }

        var age = (asOf - timestamp.Value).TotalDays;

        if (age <= 0)
        {
            return 1m;
        }

        return decimal.Round(
            Math.Clamp((decimal)Math.Pow(0.5, age / halfLifeDays), 0m, 1m), 6);
    }

    /// <summary>
    /// The overlap of two sets, as a fraction of the smaller one.
    /// <para>
    /// <b>Over the smaller set rather than the union, and the difference matters here.</b> Jaccard
    /// over the union punishes a candidate for having attributes the member has no opinion about — a
    /// richly tagged job would score lower than a sparsely tagged one purely for carrying more tags,
    /// which is exactly backwards.
    /// </para>
    /// </summary>
    public static decimal Overlap(IReadOnlySet<string> left, IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0m;
        }

        var shared = left.Count <= right.Count
            ? left.Count(right.Contains)
            : right.Count(left.Contains);

        return decimal.Round((decimal)shared / Math.Min(left.Count, right.Count), 6);
    }

    /// <summary>The largest value in a sequence, or zero for an empty one.</summary>
    public static decimal MaxOrZero(IEnumerable<decimal> values)
    {
        var maximum = 0m;

        foreach (var value in values)
        {
            if (value > maximum)
            {
                maximum = value;
            }
        }

        return maximum;
    }
}
