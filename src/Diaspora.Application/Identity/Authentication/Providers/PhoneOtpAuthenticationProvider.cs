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
/// return <c>UserErrors.InvalidCredentials</c>, which names no field on purpose.
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
/// <b>Past that point the challenge is not necessarily the caller's own.</b> The route is
/// anonymous, so anyone can post a code against a registered number without having asked for
/// one. Expired, already used and attempts exhausted are things the person holding the code
/// needs to tell apart, so the service tells them, and only them: a challenge's state is
/// disclosed only to a caller whose code matches it. This provider used to pass that state to
/// whoever asked, and a registered number with no code outstanding answered 404 where an
/// unregistered one answered 400 (found 17 Sep 2026).
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

        // Reaching here says only that the number is registered, which the caller must not learn.
        // Nobody had to request a code first, so the service is asked for the answer that tells a
        // challenge's state ("expired", "already used", "too many attempts") only to the caller
        // who typed the code it sent, and "incorrect" to everyone else.
        //
        // `OtpErrors` already words all of them. Nothing is re-written here.
        var verification = await _otpChallenges.VerifyForAnonymousCallerAsync(
            user, OtpPurpose.SignIn, credentials.Code ?? string.Empty, cancellationToken);

        return verification.IsFailure
            ? Result.Failure<AuthenticatedMember>(verification.Error)
            : Result.Success(new AuthenticatedMember(user, Method));
    }
}
