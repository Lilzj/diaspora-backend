using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Users;

namespace Diaspora.Application.Identity.Mapping;

/// <summary>
/// A member, as staff are entitled to see them.
/// <para>
/// One mapper rather than a projection per query, so the three staff reads cannot
/// disagree about what an account looks like — and so widening what staff see is one
/// edit somebody can review rather than three they might miss.
/// </para>
/// </summary>
public static class AdminMemberMapper
{
    public static AdminMemberResponse ToResponse(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new AdminMemberResponse
        {
            Id = user.Id.Value,
            FullName = user.FullName.Value,
            PhoneNumber = user.PhoneNumber.Value,
            Email = user.Email?.Value,
            Role = user.Role.Name,
            IsStaff = user.Role.IsStaff,
            AccessState = user.AccessState.Name,
            AccountStatus = user.AccountStatus.ToString(),
            IsPhoneVerified = user.IsPhoneVerified,
            Nationality = user.Nationality.Value,
            CountryOfResidence = user.CountryOfResidence.Value,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            StatusReason = user.StatusReason,
            SuspendedUntil = user.SuspendedUntil
        };
    }
}
