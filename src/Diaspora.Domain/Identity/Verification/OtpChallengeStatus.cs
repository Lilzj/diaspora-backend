namespace Diaspora.Domain.Identity.Verification;

/// <summary>
/// Lifecycle of a one-time code challenge.
/// </summary>
public enum OtpChallengeStatus
{
    /// <summary>Outstanding and redeemable.</summary>
    Pending = 0,

    /// <summary>Redeemed successfully.</summary>
    Verified = 1,

    /// <summary>The attempt budget was exhausted. A new challenge is required.</summary>
    AttemptLimitReached = 2,

    /// <summary>A newer challenge replaced this one.</summary>
    Superseded = 3
}
