using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A member added somewhere money can be sent (BSEC-007, 10 Nov 2026).
/// <para>
/// <b>Published so the member can be told, and told immediately.</b> BSEC-007's third remediation
/// step and its third verification criterion are both this: a new payout destination has to reach
/// the member in the same transaction boundary as the registration. The outbox is what makes that
/// true — the row announcing it commits with the destination row, so there is no state in which a
/// destination exists and the obligation to mention it does not.
/// </para>
/// <para>
/// <b>Not a verification event.</b> This fires when a destination is created, verified or not. A
/// member who did not add it needs to hear before it becomes payable, not after.
/// </para>
/// <para>
/// <b>The masked label and never the credential</b>, like every other Financial contract. This is
/// read by every module and stored by the outbox, and the module could not supply an account number
/// if the contract asked for one.
/// </para>
/// </summary>
public sealed record PayoutDestinationRegisteredIntegrationEvent : IntegrationEvent
{
    public required Guid DestinationId { get; init; }

    /// <summary>
    /// Whose it is. Required rather than nullable because Financial holds the owner on the
    /// aggregate and cannot fail to know it — the exception the platform's recipient rule allows
    /// for exactly this case.
    /// </summary>
    public required Guid OwnerUserId { get; init; }

    /// <summary><c>BankAccount</c>, <c>MobileMoney</c>, <c>Card</c> and so on.</summary>
    public required string Kind { get; init; }

    /// <summary>The masked, server-produced label. Never the credential behind it.</summary>
    public required string Label { get; init; }

    public required string Currency { get; init; }

    public string? Provider { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset RegisteredAt { get; init; }
}
