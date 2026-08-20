namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Ends the caller's session. The session is taken from the access token, so no
/// identifier is accepted here — a client cannot sign anyone else out.
/// </summary>
public sealed record LogoutRequest
{
    /// <summary>
    /// When true, every other session on the account is revoked as well.
    /// </summary>
    public bool RevokeAllSessions { get; init; }
}
