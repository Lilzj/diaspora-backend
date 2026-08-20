namespace Diaspora.Domain.Identity.Sessions;

/// <summary>
/// Why a session ended. Surfaced in the audit trail and in the member-facing
/// session list so a suspicious termination is explainable.
/// </summary>
public enum SessionRevocationReason
{
    /// <summary>The member signed out on this device.</summary>
    SignedOut = 0,

    /// <summary>The member revoked this session from another device.</summary>
    RevokedByUser = 1,

    /// <summary>Swept up by "sign out everywhere".</summary>
    RevokedByUserBulk = 2,

    /// <summary>The password changed, so every session was cut.</summary>
    PasswordChanged = 3,

    /// <summary>A password reset completed through account recovery.</summary>
    PasswordReset = 4,

    /// <summary>A rotated refresh token was replayed.</summary>
    TokenReuseDetected = 5,

    /// <summary>An administrator or moderation action ended the session.</summary>
    AdministrativeAction = 6,

    /// <summary>The account was suspended, locked or disabled.</summary>
    AccountStateChanged = 7,

    /// <summary>The absolute session lifetime elapsed.</summary>
    Expired = 8
}
