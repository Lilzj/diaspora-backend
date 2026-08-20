namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Updates the member-editable parts of a profile. Phone number is not among them:
/// changing it re-opens verification and is a separate flow.
/// </summary>
public sealed record UpdateProfileRequest
{
    public required string FullName { get; init; }

    public required string Nationality { get; init; }

    public required string CountryOfResidence { get; init; }

    public required string PreferredLanguage { get; init; }

    public required string PreferredTimeZone { get; init; }

    /// <summary>Optional contact address. Adding one does not grant email sign-in.</summary>
    public string? Email { get; init; }

    /// <summary>Storage key of an already-uploaded avatar.</summary>
    public string? AvatarStorageKey { get; init; }
}
