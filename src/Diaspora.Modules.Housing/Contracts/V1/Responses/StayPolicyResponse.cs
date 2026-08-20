namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>Who the property is for.</summary>
public sealed record StayPolicyResponse
{
    public required bool AcceptsShortStay { get; init; }

    public required bool AcceptsLongStay { get; init; }

    public required bool IsStudentFriendly { get; init; }

    public required bool IsFamilyFriendly { get; init; }

    public required bool IsSharedAccommodation { get; init; }

    public required bool IsLookingForRoommate { get; init; }
}
