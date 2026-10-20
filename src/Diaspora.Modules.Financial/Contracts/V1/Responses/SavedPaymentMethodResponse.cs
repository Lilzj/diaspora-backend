namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A reusable payment instrument the platform holds for a member.
/// <para>
/// <b>The platform holds none today, so every list of these is empty.</b> Card payments go through a
/// Stripe Checkout Session created with no <c>customer</c> and no <c>setup_future_usage</c>, so
/// Stripe keeps no reusable payment method for the platform; PayPal and Flutterwave are wired for
/// payouts only. The shape exists so the route answers with the truth rather than a 404, and carries
/// only what a member needs to recognise an instrument — never a card number or a provider token.
/// </para>
/// </summary>
public sealed record SavedPaymentMethodResponse
{
    public required Guid Id { get; init; }

    /// <summary>A <c>PaymentMethodKind</c>: <c>Card</c>, <c>BankTransfer</c>, <c>MobileMoney</c>…</summary>
    public required string Kind { get; init; }

    public string? Brand { get; init; }

    public string? Last4 { get; init; }

    public int? ExpiryMonth { get; init; }

    public int? ExpiryYear { get; init; }

    public required bool IsDefault { get; init; }

    public required DateTimeOffset AddedAt { get; init; }
}

/// <summary>The answer to a successful forget. Never produced while nothing is stored.</summary>
public sealed record SavedPaymentMethodRemovalResponse
{
    public required bool Deleted { get; init; }
}
