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
/// by purpose.
/// </para>
/// <para>
/// <b>Every failure here speaks about the code, never about a password.</b> This used to
/// return <c>UserErrors.InvalidCredentials</c> — "The phone number or password is incorrect."
/// — on a flow that has no password, so the one screen a member reaches by entering a
/// six-digit code told them their password was wrong.
/// </para>
/// <para>
/// The reason it did was sound and is preserved: an unknown number and a wrong code must be
/// indistinguishable, or this route becomes an oracle for which numbers are registered. The
/// fix is not to leak the difference but to move both onto the same OTP-shaped error, so the
/// wording is right for the flow **and** the two cases still read identically.
/// </para>
/// <para>
/// Past that point the challenge itself is the caller's own — they asked for it — so the
/// service's specific failure is returned unchanged: incorrect, expired, or attempts
/// exhausted are things the person holding the code needs to be able to tell apart.
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
            return Result.Failure<AuthenticatedMember>(OtpErrors.IncorrectCode);
        }

        var user = await _users.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (user is null)
        {
            // Deliberately the same error a wrong code produces. Anything more
            // specific here would say "that number is not registered", which is
            // exactly the enumeration this route must not answer.
            return Result.Failure<AuthenticatedMember>(OtpErrors.IncorrectCode);
        }

        var verification = await _otpChallenges.VerifyAsync(
            user, OtpPurpose.SignIn, credentials.Code ?? string.Empty, cancellationToken);

        // The service's own error is returned rather than flattened. Reaching here means the
        // number resolved to a member and a challenge existed, both of which the caller
        // established themselves by requesting the code — so "incorrect", "expired" and
        // "too many attempts" reveal nothing they did not already know, and are the three
        // things somebody staring at a six-digit field actually needs to tell apart.
        //
        // `OtpErrors` already words all three. Nothing is re-written here.
        return verification.IsFailure
            ? Result.Failure<AuthenticatedMember>(verification.Error)
            : Result.Success(new AuthenticatedMember(user, Method));
    }
}
