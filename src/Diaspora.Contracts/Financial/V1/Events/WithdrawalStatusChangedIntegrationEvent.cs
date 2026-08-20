using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A payout request moved.
/// <para>
/// <b>One event for the whole lifecycle rather than five.</b> A withdrawal is requested, taken up,
/// paid, refused or reversed, and every one of those is the same fact about the same request with a
/// different status on it. A consumer notifying a member wants all of them and would otherwise
/// subscribe five times; a consumer counting failures filters on one field.
/// </para>
/// <para>
/// <b>The destination is a server-issued identifier and a masked label.</b> No account number, no
/// IBAN, no sort code has ever been on this bus and none ever should be — an integration event is
/// read by every module and stored by the outbox.
/// </para>
/// </summary>
public sealed record WithdrawalStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid WithdrawalId { get; init; }

    public required Guid RequestedByUserId { get; init; }

    /// <summary>
    /// <c>Requested</c>, <c>Processing</c>, <c>Completed</c>, <c>Failed</c>, <c>Cancelled</c> or
    /// <c>Reversed</c>.
    /// </summary>
    public required string Status { get; init; }

    public required string PreviousStatus { get; init; }

    public required long AmountMinorUnits { get; init; }

    /// <summary>What the platform kept. Zero when the deployment charges nothing.</summary>
    public required long FeeMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>The masked destination label, never the credential behind it.</summary>
    public string? DestinationLabel { get; init; }

    public string? FailureCode { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
