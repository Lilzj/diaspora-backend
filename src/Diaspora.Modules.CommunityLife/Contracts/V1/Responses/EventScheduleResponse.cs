namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>When an event happens.</summary>
public sealed record EventScheduleResponse
{
    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    /// <summary>
    /// An IANA identifier. What lets a client render the local time of the place the event is at
    /// rather than the reader's — which for a diaspora platform is the whole point.
    /// </summary>
    public required string TimeZoneId { get; init; }

    public DateTimeOffset? RegistrationDeadline { get; init; }

    public DateTimeOffset? CheckInOpensAt { get; init; }
}
