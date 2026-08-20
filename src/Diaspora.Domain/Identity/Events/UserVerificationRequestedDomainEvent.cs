using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Events;

/// <summary>
/// Raised when a member asks to be verified. The Trust Engine (B3) subscribes to
/// this; Identity itself never decides the outcome.
/// </summary>
public sealed record UserVerificationRequestedDomainEvent(
    UserId UserId,
    string CountryOfResidence,
    string Nationality,
    DateTimeOffset RequestedAt) : DomainEvent;
