using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// A member added somewhere money can be sent (BSEC-007, 10 Nov 2026).
/// <para>
/// <b>PayoutDestination raised no events at all before this.</b> Adding a destination is the first
/// half of a compromised account's payout and the half the real member would recognise as wrong, so
/// it was also the single most valuable thing on the platform to tell somebody about — and it was
/// silent.
/// </para>
/// <para>
/// Carries the masked label and never the credential, for the reason the whole module is built
/// around: a domain event becomes an integration event, and an integration event is stored by the
/// outbox and read by every module.
/// </para>
/// </summary>
public sealed record PayoutDestinationRegisteredDomainEvent : DomainEvent
{
    public required PayoutDestinationId DestinationId { get; init; }

    public required Guid OwnerUserId { get; init; }

    public required PayoutDestinationKind Kind { get; init; }

    /// <summary>The label the server produced. Never a client-supplied claim, never a credential.</summary>
    public required string Label { get; init; }

    public required string Currency { get; init; }

    public string? Provider { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}
