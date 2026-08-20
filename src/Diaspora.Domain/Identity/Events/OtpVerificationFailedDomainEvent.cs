using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised on an incorrect one-time code, carrying the running attempt count.</summary>
public sealed record OtpVerificationFailedDomainEvent(
    OtpChallengeId ChallengeId,
    UserId UserId,
    OtpPurpose Purpose,
    int AttemptCount,
    bool AttemptLimitReached,
    DateTimeOffset FailedAt) : DomainEvent;
