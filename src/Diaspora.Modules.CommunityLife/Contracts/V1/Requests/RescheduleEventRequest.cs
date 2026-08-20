namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>
/// Move an event in time.
/// <para>
/// A separate command from an ordinary edit, because it is a material change: everybody who said
/// they were coming has to be told, and telling them about a corrected typo as well is how a
/// platform trains people to ignore its messages.
/// </para>
/// </summary>
public sealed record RescheduleEventRequest
{
    public int? BaseRevision { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public string? TimeZoneId { get; init; }

    public DateTimeOffset? RegistrationDeadline { get; init; }

    public DateTimeOffset? CheckInOpensAt { get; init; }

    public string? Reason { get; init; }
}
