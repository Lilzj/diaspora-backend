using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// A payment provider, as the platform sees one.
/// <para>
/// <b>Stripe, Paystack, Flutterwave, Adyen, Telr, Peach Payments and manual bank transfer are all
/// implementations of this interface, and none of them exists in this repository.</b> An
/// architecture test asserts that no type in this assembly names a payment vendor; a deployment adds
/// an adapter by registering an implementation, and nothing in this module changes.
/// </para>
/// <para>
/// <b>Four operations, because that is what a payment lifecycle needs and no more.</b> Authorize,
/// capture, cancel, refund. Tokenisation, customer records, saved instruments, payment sheets and
/// hosted pages are all things an adapter does with its own SDK on its own side; putting them here
/// would mean the platform having an opinion about how a provider works.
/// </para>
/// <para>
/// <b>An adapter declares what it supports.</b> A mobile-money provider that cannot authorise
/// separately from capturing says so through <see cref="SupportsSeparateAuthorization"/>, and the
/// payment path takes the single-step route rather than failing halfway.
/// </para>
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Whatever this adapter calls itself. Stored on the intent, never interpreted.</summary>
    string Provider { get; }

    /// <summary>The methods this adapter can move money with.</summary>
    IReadOnlySet<PaymentMethodKind> SupportedMethods { get; }

    /// <summary>The currencies this adapter accepts. Empty means it does not restrict.</summary>
    IReadOnlySet<string> SupportedCurrencies { get; }

    /// <summary>
    /// Whether the adapter can hold funds without taking them. False for most bank transfers and
    /// most mobile money, and the payment path adapts rather than failing.
    /// </summary>
    bool SupportsSeparateAuthorization { get; }

    Task<PaymentGatewayResult> AuthorizeAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default);

    Task<PaymentGatewayResult> CaptureAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default);

    Task<PaymentGatewayResult> CancelAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default);

    Task<PaymentGatewayResult> RefundAsync(
        PaymentGatewayRequest request, CancellationToken cancellationToken = default);
}
