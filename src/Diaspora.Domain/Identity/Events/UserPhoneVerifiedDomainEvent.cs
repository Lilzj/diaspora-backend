using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>Raised when the phone OTP succeeds and the member reaches limited access.</summary>
public sealed record UserPhoneVerifiedDomainEvent(
    UserId UserId,
    string PhoneNumber,
    DateTimeOffset VerifiedAt) : DomainEvent;
