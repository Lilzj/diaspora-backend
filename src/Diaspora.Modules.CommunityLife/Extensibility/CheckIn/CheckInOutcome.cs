namespace Diaspora.Modules.CommunityLife.Extensibility.CheckIn;

/// <summary>
/// What a check-in method resolved a request to.
/// <para>
/// A method's whole job is to turn "somebody presented this" into "this is which response". It does
/// not write anything: the module applies the outcome to the aggregate, which is what keeps the
/// attendance counters settled from one place however many methods exist.
/// </para>
/// </summary>
public sealed record CheckInOutcome
{
    /// <summary>Whether the method recognised the arrival.</summary>
    public required bool IsResolved { get; init; }

    /// <summary>The response to check in. Set when <see cref="IsResolved"/>.</summary>
    public Guid? RsvpId { get; init; }

    public Guid? AttendeeUserId { get; init; }

    /// <summary>Why not, when it did not. Shown to whoever is working the door.</summary>
    public string? Reason { get; init; }

    public static CheckInOutcome Unresolved(string reason) =>
        new() { IsResolved = false, Reason = reason };

    public static CheckInOutcome Resolved(Guid rsvpId, Guid attendeeUserId) =>
        new() { IsResolved = true, RsvpId = rsvpId, AttendeeUserId = attendeeUserId };
}
