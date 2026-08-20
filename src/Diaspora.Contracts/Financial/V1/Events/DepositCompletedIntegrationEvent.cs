using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A member's own balance went up because they put money into it.
/// <para>
/// <b>Published alongside <see cref="PaymentCapturedIntegrationEvent"/> rather than instead of
/// it.</b> Both are true — money arrived from outside, and it landed in a member's wallet rather
/// than in the platform's revenue — and a consumer that only wanted one of those facts would
/// otherwise have to infer it from a subject type it has no business reading. B13 sends a top-up
/// receipt off this one and nothing else; a payment receipt for a deposit would tell a member they
/// had been charged.
/// </para>
/// </summary>
public sealed record DepositCompletedIntegrationEvent : IntegrationEvent
{
    public required Guid PaymentIntentId { get; init; }

    public required Guid DepositorUserId { get; init; }

    /// <summary>The wallet that was credited. Always the depositor's own.</summary>
    public required Guid WalletId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }
}
