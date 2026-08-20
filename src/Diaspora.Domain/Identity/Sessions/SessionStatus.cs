namespace Diaspora.Domain.Identity.Sessions;

/// <summary>
/// Lifecycle of a device session.
/// </summary>
public enum SessionStatus
{
    Active = 0,
    Revoked = 1,
    Expired = 2
}
