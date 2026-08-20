using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// Somebody looked at a refund request and answered.
/// <para>
/// An approval is followed by <see cref="RefundIssuedIntegrationEvent"/> when the money actually
/// moves. They are separate because a provider can still refuse a refund the platform has approved,
/// and a member told "approved" who never sees the money needs the platform to know which of those
/// two facts it is missing.
/// </para>
/// </summary>
public sealed record RefundRequestDecidedIntegrationEvent : IntegrationEvent
{
    public required Guid RefundRequestId { get; init; }

    public required Guid PaymentIntentId { get; init; }

    public required Guid RequestedByUserId { get; init; }

    /// <summary><c>Approved</c> or <c>Rejected</c>.</summary>
    public required string Decision { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>A member-safe explanation. Never an internal note.</summary>
    public string? DecisionNote { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}
