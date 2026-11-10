using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;

namespace Diaspora.Application.Identity.Commands.Profile.UpdateProfile;

/// <summary>
/// Updates the signed-in member's profile. This is also the "complete your profile" step
/// after phone verification.
/// </summary>
[Auditable(Action = "identity.user.profile-updated")]
[RequirePermission("identity:profile:update")]
public sealed record UpdateProfileCommand : ICommand<UserProfileResponse>
{
    public required string FullName { get; init; }

    public required string Nationality { get; init; }

    public required string CountryOfResidence { get; init; }

    public required string PreferredLanguage { get; init; }

    public required string PreferredTimeZone { get; init; }

    public string? Email { get; init; }

    public string? AvatarStorageKey { get; init; }
}
