using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// One day's performance for one slice of a surface.
/// <para>
/// <b>The slice is a dimension and a key rather than a typed column, because the questions change
/// and the table should not.</b> "Which strategy converts best" is a strategy slice; "which reason
/// do members act on" is a reason slice; "does cold start work in Mauritius" is a country slice.
/// Three columns would be three migrations and a fourth question away from a fourth.
/// </para>
/// </summary>
public sealed class AnalyticsBreakdown : ValueObject
{
    private AnalyticsBreakdown(Guid breakdownId, string dimension, string key)
    {
        BreakdownId = breakdownId;
        Dimension = dimension;
        Key = key;
    }

    private AnalyticsBreakdown() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid BreakdownId { get; private set; }

    /// <summary><c>strategy</c>, <c>reason</c>, <c>targetType</c>, <c>country</c>.</summary>
    public string Dimension { get; private set; } = string.Empty;

    public string Key { get; private set; } = string.Empty;

    public long GeneratedCount { get; private set; }

    public long ViewCount { get; private set; }

    public long ClickCount { get; private set; }

    public long AcceptCount { get; private set; }

    public long DismissCount { get; private set; }

    public static AnalyticsBreakdown Create(Guid breakdownId, string dimension, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimension);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return new AnalyticsBreakdown(
            breakdownId, dimension.Trim().ToLowerInvariant(), key.Trim().ToLowerInvariant());
    }

    public void AddGenerated(long count) => GeneratedCount += Math.Max(0, count);

    public void AddViews(long count) => ViewCount += Math.Max(0, count);

    public void AddClicks(long count) => ClickCount += Math.Max(0, count);

    public void AddAccepts(long count) => AcceptCount += Math.Max(0, count);

    public void AddDismissals(long count) => DismissCount += Math.Max(0, count);

    /// <summary>Acted on, over shown. The only ratio product decisions are actually made from.</summary>
    public decimal AcceptanceRate =>
        ViewCount == 0 ? 0m : decimal.Round((decimal)AcceptCount / ViewCount, 4);

    public decimal ClickThroughRate =>
        ViewCount == 0 ? 0m : decimal.Round((decimal)ClickCount / ViewCount, 4);

    public decimal DismissalRate =>
        ViewCount == 0 ? 0m : decimal.Round((decimal)DismissCount / ViewCount, 4);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BreakdownId;
        yield return Dimension;
        yield return Key;
        yield return GeneratedCount;
        yield return ViewCount;
        yield return ClickCount;
        yield return AcceptCount;
        yield return DismissCount;
    }
}
