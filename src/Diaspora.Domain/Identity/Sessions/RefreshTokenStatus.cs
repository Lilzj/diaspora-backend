namespace Diaspora.Domain.Identity.Sessions;

/// <summary>
/// State of a single refresh token within a session's rotation chain.
/// </summary>
public enum RefreshTokenStatus
{
    /// <summary>The one token in the chain that may currently be redeemed.</summary>
    Active = 0,

    /// <summary>Redeemed and replaced. Replaying it is treated as theft.</summary>
    Rotated = 1,

    /// <summary>Invalidated without being redeemed.</summary>
    Revoked = 2,

    /// <summary>Passed its expiry without being redeemed.</summary>
    Expired = 3
}
