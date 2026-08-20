using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// Somebody started paying for something.
/// <para>
/// <b>Amounts travel as minor units and a currency, never as a decimal.</b> A published contract
/// carrying <c>2500</c> and <c>"GBP"</c> means the same thing in every consumer, in every language,
/// through every JSON parser; a decimal does not.
/// </para>
/// </summary>
public sealed record PaymentIntentCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid PaymentIntentId { get; init; }

    public required Guid PayerUserId { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    /// <summary><c>Card</c>, <c>BankTransfer</c>, <c>MobileMoney</c>, <c>Wallet</c>, <c>Credit</c>.</summary>
    public required string MethodKind { get; init; }

    /// <summary>What the payment is for, when it is for something.</summary>
    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? CountryCode { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}
