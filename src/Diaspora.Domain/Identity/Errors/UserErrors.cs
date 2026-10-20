using Diaspora.Domain.Identity.AccessStates;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Errors;

/// <summary>
/// Typed failures the <c>User</c> aggregate can return.
/// <para>
/// Authentication failures are deliberately coarse: <see cref="InvalidCredentials"/>
/// is returned for an unknown phone number and for a wrong password alike, so the
/// API cannot be used to enumerate which numbers are registered.
/// </para>
/// </summary>
public static class UserErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Identity.User.NotFound", "The requested account could not be found.");

    public static readonly Error PhoneAlreadyRegistered =
        Error.Conflict("Identity.User.PhoneAlreadyRegistered", "An account already exists for this phone number.");

    public static readonly Error EmailAlreadyRegistered =
        Error.Conflict("Identity.User.EmailAlreadyRegistered", "An account already exists for this email address.");

    /// <summary>
    /// One coarse refusal for every credential exchange.
    /// <para>
    /// <b>It names no field, and that is deliberate twice over.</b> It has to stay coarse so
    /// sign-in cannot be used to enumerate accounts — "no such address" and "wrong password" are
    /// answered identically, in timing and in words. And it is shared by password sign-in, phone OTP
    /// sign-in and device registration, so naming any one channel is wrong in the others.
    /// </para>
    /// <para>
    /// It used to read "The phone number or password is incorrect", written when sign-in was
    /// phone-based. After the move to email and password it told members to check a phone number the
    /// form does not ask for.
    /// </para>
    /// </summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Identity.User.InvalidCredentials", "Those sign-in details are not right.");

    public static readonly Error PhoneNotVerified =
        Error.Forbidden("Identity.User.PhoneNotVerified", "The phone number on this account has not been verified.");

    public static readonly Error PhoneAlreadyVerified =
        Error.Conflict("Identity.User.PhoneAlreadyVerified", "The phone number on this account is already verified.");

    public static readonly Error EmailVerificationAddressChanged =
        Error.Conflict("Identity.User.EmailVerificationAddressChanged", "This link was sent to an email address that is no longer on the account.");

    public static readonly Error EmailChangeSameAddress =
        Error.Validation("Identity.User.EmailChangeSameAddress", "That is already the email address on your account.");

    public static readonly Error NoEmailOnAccount =
        Error.Validation("Identity.User.NoEmailOnAccount", "There is no email address on this account to verify.");

    public static readonly Error EmailNotVerified =
        Error.Forbidden("Identity.User.EmailNotVerified", "The email address on this account has not been verified.");

    public static readonly Error EmailAlreadyVerified =
        Error.Conflict("Identity.User.EmailAlreadyVerified", "The email address on this account is already verified.");

    public static readonly Error AccountLocked =
        Error.Forbidden("Identity.User.AccountLocked", "This account is locked. Complete account recovery to restore access.");

    public static readonly Error AccountDisabled =
        Error.Forbidden("Identity.User.AccountDisabled", "This account has been disabled.");

    public static readonly Error AuthenticationNotAllowed =
        Error.Forbidden("Identity.User.AuthenticationNotAllowed", "This account cannot sign in in its current state.");

    public static readonly Error SamePassword =
        Error.Validation("Identity.User.SamePassword", "The new password must differ from the current password.");

    public static readonly Error PasswordPreviouslyUsed =
        Error.Validation("Identity.User.PasswordPreviouslyUsed", "This password has been used recently. Choose a different one.");

    public static readonly Error IncorrectCurrentPassword =
        Error.Validation("Identity.User.IncorrectCurrentPassword", "The current password is incorrect.");

    public static readonly Error VerificationAlreadyRequested =
        Error.Conflict("Identity.User.VerificationAlreadyRequested", "A verification request is already in progress.");

    public static readonly Error NotEligibleForVerification =
        Error.Forbidden("Identity.User.NotEligibleForVerification", "This account cannot request verification in its current state.");

    public static Error AuthenticationMethodNotSupported(string method) =>
        Error.Validation(
            "Identity.User.AuthenticationMethodNotSupported",
            $"'{method}' is not a sign-in method this platform offers.");

    public static Error TemporarilyLockedOut(TimeSpan retryAfter) =>
        Error.RateLimited(
            "Identity.User.TemporarilyLockedOut",
            $"Too many failed sign-in attempts. Try again in {Math.Ceiling(retryAfter.TotalSeconds)} seconds.");

    public static Error InvalidStateTransition(AccessState from, AccessState to) =>
        Error.Conflict(
            "Identity.User.InvalidStateTransition",
            $"An account cannot move from '{from.Name}' to '{to.Name}'.");
}
