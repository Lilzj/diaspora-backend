using Diaspora.SharedKernel.Results;

namespace Diaspora.Domain.Identity.Errors;

/// <summary>
/// Typed failures raised by the <c>UserSession</c> aggregate and refresh-token rotation.
/// </summary>
public static class SessionErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Identity.Session.NotFound", "The requested session could not be found.");

    public static readonly Error NotOwnedByUser =
        Error.Forbidden("Identity.Session.NotOwnedByUser", "The session does not belong to the current account.");

    public static readonly Error AlreadyRevoked =
        Error.Conflict("Identity.Session.AlreadyRevoked", "The session has already been revoked.");

    public static readonly Error NotActive =
        Error.Unauthorized("Identity.Session.NotActive", "The session is no longer active. Sign in again.");

    public static readonly Error Expired =
        Error.Unauthorized("Identity.Session.Expired", "The session has expired. Sign in again.");

    public static readonly Error RefreshTokenNotFound =
        Error.Unauthorized("Identity.Session.RefreshTokenNotFound", "The refresh token is not valid.");

    public static readonly Error RefreshTokenExpired =
        Error.Unauthorized("Identity.Session.RefreshTokenExpired", "The refresh token has expired. Sign in again.");

    /// <summary>
    /// Returned when a token that was already rotated or revoked is presented again.
    /// The session is torn down before this is returned — see
    /// <c>UserSession.RotateRefreshToken</c>.
    /// </summary>
    public static readonly Error RefreshTokenReuseDetected =
        Error.Unauthorized(
            "Identity.Session.RefreshTokenReuseDetected",
            "This refresh token has already been used. All sessions on this device have been revoked as a precaution.");

    public static readonly Error CannotRevokeCurrentSession =
        Error.Validation(
            "Identity.Session.CannotRevokeCurrentSession",
            "Use sign-out to end the session you are currently using.");
}
