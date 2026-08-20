namespace Diaspora.Domain.Identity.AccessStates;

/// <summary>
/// Coarse account lifecycle status, derived from <see cref="AccessState"/>.
/// Never stored independently — see <see cref="AccessState.ToAccountStatus"/>.
/// </summary>
public enum AccountStatus
{
    /// <summary>Registration or phone verification has not finished.</summary>
    Provisioning = 0,

    /// <summary>Usable account, at whatever access level the ladder has reached.</summary>
    Active = 1,

    /// <summary>Suspended by moderation.</summary>
    Suspended = 2,

    /// <summary>Hard-locked for security reasons; requires account recovery.</summary>
    Locked = 3,

    /// <summary>Closed.</summary>
    Disabled = 4
}
