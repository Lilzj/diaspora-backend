using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>
/// Raised when a member confirms the email address on their account for the first time.
/// <para>
/// Alongside, not instead of, the <see cref="UserProfileUpdatedDomainEvent"/> naming
/// <c>EmailVerified</c>: that one is for the audit trail of what changed on a profile, and a
/// consumer reading a list of field names to discover a verification is the inference
/// <c>UserPhoneVerifiedIntegrationEvent</c> exists to avoid.
/// </para>
/// </summary>
public sealed record UserEmailVerifiedDomainEvent(
    UserId UserId,
    string EmailAddress,
    DateTimeOffset VerifiedAt) : DomainEvent;
