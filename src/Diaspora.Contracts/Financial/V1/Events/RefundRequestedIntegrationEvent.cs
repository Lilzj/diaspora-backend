using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A member asked for their money back. Nothing has moved.
/// <para>
/// <b>A request is not a refund, and keeping the two events apart is the point of the aggregate
/// behind them.</b> <see cref="RefundIssuedIntegrationEvent"/> means money went back; this means
/// somebody would like it to. A consumer that treated them as one would tell a member their refund
/// had arrived the moment they asked for it.
/// </para>
/// </summary>
public sealed record RefundRequestedIntegrationEvent : IntegrationEvent
{
    public required Guid RefundRequestId { get; init; }

    public required Guid PaymentIntentId { get; init; }

    public required Guid RequestedByUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required string Reason { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
