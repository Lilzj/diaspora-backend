using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// Picks the adapter for a payment.
/// <para>
/// <b>Routing is data, not a switch statement.</b> A deployment operating in three countries with
/// three providers configures which one handles what; the alternative — a chain of
/// <c>if (country == "NG")</c> in a handler — is the shape that makes adding a fourth provider a
/// code change in the payment path.
/// </para>
/// <para>
/// The default implementation resolves by declared support and configured preference order, and is
/// replaceable like every other single-implementation seam here.
/// </para>
/// </summary>
public interface IPaymentGatewayRegistry
{
    /// <summary>The adapter that can take this payment, or null when none can.</summary>
    IPaymentGateway? Resolve(PaymentMethodKind methodKind, string currency, string? countryCode);

    /// <summary>The adapter with this name, or null. How a webhook finds its own.</summary>
    IPaymentGateway? Find(string provider);

    /// <summary>Everything registered. What the capabilities endpoint reports.</summary>
    IReadOnlyList<IPaymentGateway> All { get; }
}
