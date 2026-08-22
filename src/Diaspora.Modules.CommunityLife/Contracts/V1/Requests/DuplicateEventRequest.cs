namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// What a caller may say about the copy — BG-07.
/// <para>
/// Two optional fields, and the body itself is optional. See
/// <c>DuplicateEventCommandHandler</c> for what a duplicate carries, and for why the schedule is
/// the one thing it cannot simply inherit.
/// </para>
/// </summary>
public sealed record DuplicateEventRequest
{
    /// <summary>A title for the copy. Null takes the source's, prefixed <c>Copy of </c>.</summary>
    public string? Title { get; init; }

    /// <summary>
    /// When the copy starts. Null shifts the source's schedule forward in whole weeks until it is
    /// in the future, which keeps the event's weekday and time of day — what a community event that
    /// repeats actually wants.
    /// </summary>
    public DateTimeOffset? StartsAt { get; init; }
}
