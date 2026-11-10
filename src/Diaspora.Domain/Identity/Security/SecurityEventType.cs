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
    TrustDecisionApplied = 25,

    /// <summary>
    /// A member was given a platform role, or had one taken away.
    /// <para>
    /// Its own type rather than <see cref="AccessStateChanged"/>: what somebody may do as
    /// staff and what state their own account is in are different facts, and the day
    /// somebody asks who made whom a moderator, the answer should not require reading
    /// descriptions.
    /// </para>
    /// </summary>
    RoleAssigned = 26,

    /// <summary>
    /// A member asked to move their sign-in address. The address has not changed yet: that waits
    /// for the link sent to the new one.
    /// </summary>
    EmailChangeRequested = 27,

    /// <summary>The sign-in address was replaced after the new one was proved.</summary>
    EmailChanged = 28,

    /// <summary>
    /// A session re-proved the member's identity and earned a step-up grant (BSEC-007).
    /// <para>
    /// Its own type rather than <see cref="PhoneVerified"/> or <see cref="LoginSucceeded"/>,
    /// because the question it answers is neither of theirs. "When did this member last confirm,
    /// from which session, for what" is the first question of any dispute about a payout, and
    /// filing it under a verification event would bury it among registration codes.
    /// </para>
    /// <para>
    /// There is deliberately no matching refusal event. The gate refuses in the MediatR pipeline,
    /// outside any unit of work, and a refusal that had to open one to record itself would make the
    /// cheap path expensive; the refusal is already in the request log with its own error code.
    /// </para>
    /// </summary>
    StepUpGranted = 29
}
