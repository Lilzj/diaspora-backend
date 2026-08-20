namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Revise an event. Omitted fields are left alone.</summary>
public sealed record UpdateEventRequest
{
    public int? BaseRevision { get; init; }

    public string? Title { get; init; }

    public string? Summary { get; init; }

    public string? Description { get; init; }

    public string? CategoryKey { get; init; }

    public string? HostName { get; init; }

    public int? Capacity { get; init; }

    public bool? RequiresRsvp { get; init; }

    /// <summary>
    /// Whether people who are not going may see who is. Off by default — an attendee list is the
    /// most socially sensitive thing this module holds.
    /// </summary>
    public bool? IsAttendeeListPublic { get; init; }

    public bool? IsFamilyFriendly { get; init; }

    public bool? IsChildFriendly { get; init; }

    public bool? IsStudentFocused { get; init; }

    public bool? IsFree { get; init; }

    public string? DressCode { get; init; }

    public string? AccessibilityNotes { get; init; }

    public ContactDetailsRequest? Contact { get; init; }

    /// <summary><c>Private</c>, <c>Public</c>, <c>MembersOnly</c>, <c>Unlisted</c>.</summary>
    public string? Visibility { get; init; }
}
