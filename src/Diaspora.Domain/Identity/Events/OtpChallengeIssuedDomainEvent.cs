using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when a one-time code is created or resent.</summary>
public sealed record OtpChallengeIssuedDomainEvent(
    OtpChallengeId ChallengeId,
    UserId UserId,
    string PhoneNumber,
    OtpPurpose Purpose,
    int ResendCount,
    DateTimeOffset ExpiresAt) : DomainEvent;
