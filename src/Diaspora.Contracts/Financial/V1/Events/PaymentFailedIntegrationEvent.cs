using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A payment did not go through.
/// <para>
/// <b><see cref="FailureCode"/> is the provider's own and is deliberately not translated.</b> A code
/// table mapping every provider's decline reasons onto a platform vocabulary would be provider
/// knowledge in a module that has none, and it would be wrong for the next provider.
/// <see cref="IsRetryable"/> is the platform's own judgement and is the field a consumer should act
/// on.
/// </para>
/// </summary>
public sealed record PaymentFailedIntegrationEvent : IntegrationEvent
{
    public required Guid PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    public required bool IsRetryable { get; init; }

    public required DateTimeOffset FailedAt { get; init; }
}
