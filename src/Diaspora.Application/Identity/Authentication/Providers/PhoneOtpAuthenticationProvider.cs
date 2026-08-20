using Diaspora.Application.Identity.Abstractions;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Results;
using Diaspora.SharedKernel.ValueObjects;

namespace Diaspora.Application.Identity.Authentication.Providers;

/// <summary>
/// Phone number and a one-time code — the frontend's primary sign-in.
/// <para>
/// The code must have been minted for <see cref="OtpPurpose.SignIn"/>: a registration or
/// recovery code cannot be replayed to sign in, because the OTP store scopes every lookup
/// by purpose. The same coarse failure the password provider returns is used here for an
/// unknown number and a wrong code alike, so the two methods are equally silent about which
/// numbers are registered.
/// </para>
/// </summary>
public sealed class PhoneOtpAuthenticationProvider : IAuthenticationProvider
{
    private readonly IUserRepository _users;
    private readonly IOtpChallengeService _otpChallenges;

    public PhoneOtpAuthenticationProvider(IUserRepository users, IOtpChallengeService otpChallenges)
    {
        _users = users;
        _otpChallenges = otpChallenges;
    }

    public AuthenticationMethod Method => AuthenticationMethod.PhoneOtp;

    public async Task<Result<AuthenticatedMember>> AuthenticateAsync(
        AuthenticationCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (!PhoneNumber.TryCreate(credentials.PhoneNumber, out var phoneNumber) || phoneNumber is null)
        {
            return Result.Failure<AuthenticatedMember>(UserErrors.InvalidCredentials);
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            return Result.Failure<AuthenticatedMember>(UserErrors.InvalidCredentials);
        }

        var verification = await _otpChallenges.VerifyAsync(
            user, OtpPurpose.SignIn, credentials.Code ?? string.Empty, cancellationToken);

        // The OTP service already consumes an attempt on failure, which is the brute-force
        // defence. Collapse its specific error into the coarse one so this path leaks no
        // more than the password path does.
        return verification.IsFailure
            ? Result.Failure<AuthenticatedMember>(UserErrors.InvalidCredentials)
            : Result.Success(new AuthenticatedMember(user, Method));
    }
}
