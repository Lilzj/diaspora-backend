namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Replaces one day's schedule.
/// <para>
/// Wholesale rather than per-period, because the client's hours editor is a week of rows and a
/// person edits a day. It also makes the overlap check trivially correct: the periods are
/// validated against each other as a set, rather than each against whatever happened to be
/// stored when it was added.
/// </para>
/// <para>
/// <see cref="IsClosed"/> with no windows is <b>not</b> the same as no windows at all. The first
/// says "we do not open on Sundays"; the second says nothing, and a client should render nothing.
/// </para>
/// </summary>
public sealed record OperatingHoursRequest
{
    /// <summary>"Monday". The client's <c>dayOfWeek</c>, spelled identically.</summary>
    public required string DayOfWeek { get; init; }

    public bool IsClosed { get; init; }

    /// <summary>Several are allowed — a split shift is two.</summary>
    public IReadOnlyList<AvailabilityWindowEntry> Windows { get; init; } = [];
}
