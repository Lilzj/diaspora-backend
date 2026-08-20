namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>
/// Changes the password of the signed-in member.
/// <para>
/// Every other session is revoked; the caller's own session is kept. The endpoint returns
/// an acknowledgement, not a new token pair — but the caller's <em>current</em> access
/// token stops validating almost immediately, because changing the password bumps the
/// security stamp and the per-request validator rejects any token carrying the old one. A
/// client should refresh after a successful change; its refresh token, tied to the surviving
/// session, still works.
/// </para>
/// </summary>
public sealed record ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }

    public required string NewPassword { get; init; }
}
