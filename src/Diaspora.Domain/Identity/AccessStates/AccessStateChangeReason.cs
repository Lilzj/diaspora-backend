namespace Diaspora.Domain.Identity.AccessStates;

/// <summary>
/// Why an access state changed. Recorded on the state-change domain event so the
/// audit trail explains every movement on the ladder, not just that it happened.
/// </summary>
public enum AccessStateChangeReason
{
    Registration = 0,
    PhoneVerified = 1,
    VerificationRequested = 2,
    VerificationReviewStarted = 3,
    TrustDecisionGranted = 4,
    TrustDecisionRejected = 5,
    TrustRevoked = 6,
    ModerationSuspension = 7,
    ModerationReinstatement = 8,
    SecurityLock = 9,
    AccountRecovery = 10,
    AccountDisabled = 11,
    AccountReactivated = 12
}
