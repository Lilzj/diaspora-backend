namespace Diaspora.Domain.Identity.Verification;

/// <summary>
/// What a one-time code authorises. A code minted for one purpose can never be
/// redeemed for another — lookups are always scoped by purpose.
/// </summary>
public enum OtpPurpose
{
    /// <summary>Confirms the phone number given at registration.</summary>
    PhoneVerification = 0,

    /// <summary>Authorises a password reset through account recovery.</summary>
    PasswordReset = 1,

    /// <summary>Authorises recovery of a hard-locked account.</summary>
    AccountRecovery = 2,

    /// <summary>Re-confirms identity before a sensitive action. Reserved for a future step-up flow.</summary>
    StepUpAuthentication = 3,

    /// <summary>
    /// Authorises a passwordless sign-in. The frontend's primary sign-in is a code sent to
    /// the member's phone; this is the purpose that code is minted under, kept distinct
    /// from <see cref="PhoneVerification"/> so a registration code can never be replayed as
    /// a sign-in and vice versa.
    /// </summary>
    SignIn = 4
}
