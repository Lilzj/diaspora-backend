namespace Diaspora.Application.Identity.Auditing;

/// <summary>
/// The audit vocabulary for identity. Stable strings, because audit records outlive the
/// code that wrote them and must stay readable after a refactor renames a handler.
/// </summary>
public static class IdentityAuditActions
{
    public const string UserRegistered = "identity.user.registered";
    public const string PhoneVerified = "identity.user.phone-verified";
    public const string UserLoggedIn = "identity.user.logged-in";
    public const string UserLoggedOut = "identity.user.logged-out";
    public const string LoginFailed = "identity.user.login-failed";
    public const string TokenRefreshed = "identity.user.token-refreshed";
    public const string PasswordChanged = "identity.user.password-changed";
    public const string PasswordResetRequested = "identity.user.password-reset-requested";
    public const string PasswordResetCompleted = "identity.user.password-reset-completed";
    public const string ProfileUpdated = "identity.user.profile-updated";
    public const string SessionRevoked = "identity.session.revoked";
    public const string AllSessionsRevoked = "identity.session.revoked-all";
    public const string AccountLocked = "identity.user.locked";
    public const string AccountSuspended = "identity.user.suspended";
    public const string AccountReinstated = "identity.user.reinstated";
    public const string AccountDisabled = "identity.user.disabled";
    public const string AccessStateChanged = "identity.user.access-state-changed";
    public const string VerificationRequested = "identity.user.verification-requested";
    public const string TrustDecisionApplied = "identity.user.trust-decision-applied";
    public const string DeviceRegistered = "identity.device.registered";

    /// <summary>Entity type names used alongside the actions above.</summary>
    public static class EntityTypes
    {
        public const string User = "IdentityUser";
        public const string Session = "IdentitySession";
        public const string Device = "IdentityDevice";
        public const string OtpChallenge = "IdentityOtpChallenge";
    }
}
