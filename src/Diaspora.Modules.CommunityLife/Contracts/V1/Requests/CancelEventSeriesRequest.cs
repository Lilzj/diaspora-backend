namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Call a repeating event off.
/// <para>
/// Every future date goes with it, each releasing its own places and telling its own attendees.
/// Dates already held are left alone: an event that happened cannot be un-happened, and rewriting
/// its attendance would erase the record of who came.
/// </para>
/// </summary>
public sealed record CancelEventSeriesRequest
{
    public string? Reason { get; init; }

    /// <summary>
    /// Stop generating new dates but leave the ones already announced standing. What a committee
    /// breaking for the summer means, as distinct from one winding up.
    /// </summary>
    public bool PauseOnly { get; init; }
}
