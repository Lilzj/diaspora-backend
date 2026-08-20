namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Move or call off one date of a repeating event.
/// <para>
/// Both acts produce an exception on the rule, and that is what makes them stick: without one, the
/// next expansion would regenerate the date and everybody who had been told the meeting was off
/// would be re-invited to it.
/// </para>
/// </summary>
public sealed record UpdateOccurrenceRequest
{
    /// <summary>
    /// <c>Cancelled</c> to call the date off, or omitted to move it.
    /// <para>
    /// There is deliberately no way back to <c>Scheduled</c>. Cancelling releases every place and
    /// tells everybody who held one; un-cancelling would put the date back on their calendar without
    /// putting their place back, and the honest way to run it again is a new date.
    /// </para>
    /// </summary>
    public string? Status { get; init; }

    /// <summary>The new local date, in the series' own zone. Required when moving.</summary>
    public DateOnly? Date { get; init; }

    /// <summary>The new local start time. Defaults to the series' own when only the date moves.</summary>
    public TimeOnly? StartTime { get; init; }

    /// <summary>How long this one date runs. Defaults to the series' own duration.</summary>
    public int? DurationMinutes { get; init; }

    public int? BaseRevision { get; init; }

    public string? Reason { get; init; }
}
