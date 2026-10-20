namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// The signed-in member's own profile. Phone number is masked; the raw value is never
/// echoed back over the wire.
/// </summary>
public sealed record UserProfileResponse
{
    public required Guid Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    public string? Email { get; init; }

    public required string Nationality { get; init; }

    public required string CountryOfResidence { get; init; }

    public required string PreferredLanguage { get; init; }

    public required string PreferredTimeZone { get; init; }

    public string? AvatarStorageKey { get; init; }

    /// <summary>
    /// The profile photo, absolute and unsigned, ready for an image tag; null when there is none
    /// or the stored key is not a Media asset (set by the older free-text profile field).
    /// </summary>
    public string? AvatarUrl { get; init; }

    public required string Role { get; init; }

    public required string AccessState { get; init; }

    public required string AccountStatus { get; init; }

    public required bool IsPhoneVerified { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? LastLoginAt { get; init; }

    public DateTimeOffset? LastActiveAt { get; init; }
}
