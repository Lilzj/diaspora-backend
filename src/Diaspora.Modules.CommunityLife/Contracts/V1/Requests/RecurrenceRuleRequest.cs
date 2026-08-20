namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// How often a series repeats.
/// <para>
/// Field names are RFC 5545's, so the contract is a standard rather than an invention. The server
/// answers with the normalised rule and its own <c>description</c>, and a client should render that
/// description rather than composing a sentence of its own — the semantics live here, and a sentence
/// written beside a different set of rules drifts from them.
/// </para>
/// </summary>
public sealed record RecurrenceRuleRequest
{
    /// <summary>
    /// <c>Daily</c>, <c>Weekly</c>, <c>Monthly</c> or <c>Yearly</c>.
    /// <para>
    /// <c>Custom</c> is accepted and normalised to <c>Weekly</c>, which is what it means once
    /// <see cref="ByWeekday"/> is set. The response carries the normalised value.
    /// </para>
    /// </summary>
    public required string Frequency { get; init; }

    /// <summary>Every N periods. One means every week; two means every other week.</summary>
    public int Interval { get; init; } = 1;

    /// <summary>
    /// ISO weekday numbers — 1 is Monday, 7 is Sunday. Weekly rules only. Empty means "the weekday
    /// the series starts on".
    /// </summary>
    public IReadOnlyList<int>? ByWeekday { get; init; }

    /// <summary>
    /// Days of the month, 1 to 31, or negative counting back from the end so -1 is the last day.
    /// Monthly rules only. A day a month does not have is skipped in that month rather than moved.
    /// </summary>
    public IReadOnlyList<int>? ByMonthDay { get; init; }

    /// <summary><c>Never</c>, <c>OnDate</c> or <c>AfterCount</c>.</summary>
    public string? EndKind { get; init; }

    /// <summary>The last date an occurrence may start on. Required when <c>endKind</c> is OnDate.</summary>
    public DateOnly? Until { get; init; }

    /// <summary>How many occurrences in total. Required when <c>endKind</c> is AfterCount.</summary>
    public int? Count { get; init; }

    /// <summary>
    /// Local dates the rule generates and the organizer does not want.
    /// <para>
    /// Dates in the series' own zone, never instants: an exception is a statement about the
    /// calendar, and a UTC instant would remove the wrong day for an evening event east of Greenwich.
    /// </para>
    /// </summary>
    public IReadOnlyList<DateOnly>? Exceptions { get; init; }
}
