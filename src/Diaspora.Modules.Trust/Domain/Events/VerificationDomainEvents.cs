using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain.Events;

/// <summary>
/// Domain events raised by the verification aggregate.
/// <para>
/// These are in-process facts, dispatched by the platform's
/// <c>IDomainEventDispatcher</c> after the aggregate is saved. They are deliberately
/// separate from the module's <em>integration</em> events: a domain event carries the
/// module's own types and may change freely, while an integration event is a published
/// contract other services parse. Handlers translate one into the other at the boundary,
/// which is what lets the outbound contract stay frozen while the domain keeps moving.
/// </para>
/// </summary>
public sealed record VerificationSubmittedDomainEvent(
    VerificationRequestId RequestId,
    UserId UserId,
    VerificationType Type,
    string? Nationality,
    string? ResidenceCountry,
    string? TargetCountry,
    DateTimeOffset At) : DomainEvent;

/// <summary>Raised on every legal status move, including ones with a richer event beside them.</summary>
public sealed record VerificationStatusChangedDomainEvent(
    VerificationRequestId RequestId,
    UserId UserId,
    VerificationType Type,
    VerificationStatus From,
    VerificationStatus To,
    DateTimeOffset At) : DomainEvent;

public sealed record VerificationApprovedDomainEvent(
    VerificationRequestId RequestId,
    UserId UserId,
    VerificationType Type,
    string? TargetCountry,
    bool Partial,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset At) : DomainEvent;

/// <summary>
/// Raised when a verification is refused, by a reviewer or by a rule.
/// <para>
/// <b>B3.0.1</b> added <paramref name="TargetCountry"/>. The aggregate has always known which
/// community a country claim was about; the refusal event simply never carried it forward, so
/// a consumer holding a per-country projection had nothing to attribute the refusal to.
/// </para>
/// </summary>
public sealed record VerificationRejectedDomainEvent(
    VerificationRequestId RequestId,
    UserId UserId,
    VerificationType Type,
    string? TargetCountry,
    VerificationRejectionReason Reason,
    string? Details,
    DateTimeOffset At) : DomainEvent;

public sealed record VerificationMoreInformationRequestedDomainEvent(
    VerificationRequestId RequestId,
    UserId UserId,
    VerificationType Type,
    string Details,
    DateTimeOffset At) : DomainEvent;

/// <summary>
/// Raised when a granted verification lapses on its own expiry.
/// <para><b>B3.0.1</b> added <paramref name="TargetCountry"/>.</para>
/// </summary>
public sealed record VerificationExpiredDomainEvent(
    VerificationRequestId RequestId,
    UserId UserId,
    VerificationType Type,
    string? TargetCountry,
    DateTimeOffset At) : DomainEvent;

/// <summary>
/// Raised when a granted verification is withdrawn for cause.
/// <para><b>B3.0.1</b> added <paramref name="TargetCountry"/>.</para>
/// </summary>
public sealed record VerificationRevokedDomainEvent(
    VerificationRequestId RequestId,
    UserId UserId,
    VerificationType Type,
    string? TargetCountry,
    string Reason,
    DateTimeOffset At) : DomainEvent;
