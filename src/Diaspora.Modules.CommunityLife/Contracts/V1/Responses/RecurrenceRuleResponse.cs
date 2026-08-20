namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>
/// A recurrence rule, normalised.
/// <para>
/// What comes back is what was stored, not what was sent: a <c>Custom</c> frequency returns as
/// <c>Weekly</c>, day lists return sorted and de-duplicated, and a weekday list on a monthly rule
/// returns empty. An editor rendering this sees the rule the server will actually expand.
/// </para>
/// </summary>
public sealed record RecurrenceRuleResponse
{
    public required string Frequency { get; init; }

    public required int Interval { get; init; }

    /// <summary>ISO weekday numbers — 1 is Monday, 7 is Sunday.</summary>
    public IReadOnlyList<int> ByWeekday { get; init; } = [];

    public IReadOnlyList<int> ByMonthDay { get; init; } = [];

    public required string EndKind { get; init; }

    public DateOnly? Until { get; init; }

    public int? Count { get; init; }

    /// <summary>Local dates the rule generates and the series no longer holds.</summary>
    public IReadOnlyList<DateOnly> Exceptions { get; init; } = [];

    /// <summary>
    /// The rule in words, rendered by the server.
    /// <para>
    /// The client shows this in preference to composing its own sentence: the semantics live on the
    /// server, and a sentence written next to a different set of rules drifts from them. English
    /// only for now — see the B12.1 report's technical-debt section.
    /// </para>
    /// </summary>
    public required string Description { get; init; }
}
