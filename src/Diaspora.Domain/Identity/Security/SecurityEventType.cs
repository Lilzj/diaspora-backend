namespace Diaspora.Domain.Identity.Security;

/// <summary>
/// The catalogue of security-relevant occurrences Identity records. Distinct from
/// the audit trail: audit answers "who changed what", security events answer
/// "what happened to this account, and should anyone be worried".
/// </summary>
public enum SecurityEventType
{
    RegistrationCompleted = 0,
    LoginSucceeded = 1,
    LoginFailed = 2,
    LoginBlocked = 3,
    TemporaryLockoutTriggered = 4,
    LogoutSucceeded = 5,
    TokenRefreshed = 6,
    RefreshTokenReuseDetected = 7,
    SessionRevoked = 8,
    AllSessionsRevoked = 9,
    PasswordChanged = 10,
    PasswordResetRequested = 11,
    PasswordResetCompleted = 12,
    PhoneVerified = 13,
    OtpIssued = 14,
    OtpVerificationFailed = 15,
    OtpAttemptLimitReached = 16,
    AccessStateChanged = 17,
    AccountSuspended = 18,
    AccountLocked = 19,
    AccountDisabled = 20,
    AccountReinstated = 21,
    ProfileUpdated = 22,
    DeviceRegistered = 23,
    VerificationRequested = 24,
    TrustDecisionApplied = 25
}
