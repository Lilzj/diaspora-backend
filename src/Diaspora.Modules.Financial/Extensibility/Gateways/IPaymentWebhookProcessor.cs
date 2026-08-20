namespace Diaspora.Modules.Financial.Extensibility.Gateways;

/// <summary>
/// Turns a provider's callback into something the platform can act on.
/// <para>
/// <b>Separate from <see cref="IPaymentGateway"/> on purpose.</b> Some providers verify callbacks
/// with an entirely different key, some send them from a different service, and some deployments
/// terminate them at a gateway that has already verified them. One interface doing both would force
/// every adapter to implement a half it may not own.
/// </para>
/// <para>
/// <b>Verification is the processor's job and it must never be optional.</b> An unverified callback
/// is an unauthenticated request that moves money; the endpoint refuses anything whose
/// <c>IsVerified</c> is false, without reading the rest.
/// </para>
/// </summary>
public interface IPaymentWebhookProcessor
{
    /// <summary>Which provider's callbacks this processes.</summary>
    string Provider { get; }

    Task<PaymentWebhookResult> ProcessAsync(
        PaymentWebhookEnvelope envelope, CancellationToken cancellationToken = default);
}
