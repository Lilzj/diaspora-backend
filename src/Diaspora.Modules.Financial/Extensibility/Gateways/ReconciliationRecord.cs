namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// One line of a provider's own record of what it did.
/// <para>
/// The month-end question: does what the platform believes match what the provider says. Fees are
/// on this record and not on the payment intent, because a provider's fee is usually known only when
/// it settles — which is exactly why <c>Settled</c> is a distinct state from <c>Captured</c>.
/// </para>
/// </summary>
public sealed record ReconciliationRecord
{
    public required string Provider { get; init; }

    public required string ExternalReference { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary>What the provider kept. Null when it does not report one per transaction.</summary>
    public long? FeeMinorUnits { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset SettledAt { get; init; }
}
