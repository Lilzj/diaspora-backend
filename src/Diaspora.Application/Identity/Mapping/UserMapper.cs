using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// Projects the <see cref="User"/> aggregate onto its wire representation.
/// <para>
/// Written by hand rather than left to convention mapping. Two of these projections are
/// security decisions — the phone number is masked and the password hash, security stamp
/// and password history have no representation at all — and a convention-based mapper
/// would happily expose them the moment someone adds a property.
/// </para>
/// </summary>
public static class UserMapper
{
    /// <param name="user">The member.</param>
    /// <param name="avatars">
    /// Supplies <c>avatarUrl</c>. Optional so the Mapster projection keeps working; a response built
    /// without it carries the key and no address.
    /// </param>
    public static UserProfileResponse ToProfileResponse(User user, IAvatarUrlResolver? avatars = null)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserProfileResponse
        {
            Id = user.Id.Value,
            FullName = user.FullName.Value,
            PhoneNumber = user.PhoneNumber.Masked,
            Email = user.Email?.Value,
            Nationality = user.Nationality.Value,
            CountryOfResidence = user.CountryOfResidence.Value,
            PreferredLanguage = user.PreferredLanguage.Value,
            PreferredTimeZone = user.PreferredTimeZone.Value,
            AvatarStorageKey = user.Avatar?.StorageKey,
            AvatarUrl = avatars?.Resolve(user.Avatar?.StorageKey),
            Role = user.Role.Name,
            AccessState = user.AccessState.Name,
            AccountStatus = user.AccountStatus.ToString(),
            IsPhoneVerified = user.IsPhoneVerified,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            LastActiveAt = user.LastActiveAt
        };
    }
}
