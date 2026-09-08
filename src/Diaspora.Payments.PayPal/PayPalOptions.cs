namespace Diaspora.Payments.PayPal;

/// <summary>
/// What a deployment tells the PayPal adapter.
/// <para>
/// <b>Two credentials rather than one, and neither has a default.</b> PayPal authenticates a client
/// id and secret pair for an access token rather than presenting a key per request, so both belong
/// in user-secrets or a vault. An adapter missing either is not registered at all, and the payout
/// router then refuses with <c>financial.payout.provider_not_registered</c> rather than a 401 on
/// somebody's withdrawal.
/// </para>
/// <para>
/// <b>Sandbox and live are different hosts, not different keys.</b> Unlike Stripe, where the key
/// prefix says which environment it belongs to, PayPal's credentials look identical and only
/// <see cref="ApiBase"/> decides where they are sent. <see cref="IsSandbox"/> reads the host, and
/// the registration logs it, because a deployment that believed it was in sandbox and was not would
/// find out by moving somebody's money.
/// </para>
/// </summary>
public sealed class PayPalOptions
{
    public const string SectionName = "Payments:PayPal";

    /// <summary>PayPal's sandbox host. The default, so an unconfigured host cannot pay anybody real.</summary>
    public const string SandboxApiBase = "https://api-m.sandbox.paypal.com";

    /// <summary>PayPal's live host. Set deliberately, never by leaving something out.</summary>
    public const string LiveApiBase = "https://api-m.paypal.com";

    /// <summary>The name this adapter answers to, and the value the capability matrix must use.</summary>
    public string Provider { get; set; } = "paypal";

    /// <summary>The REST app's client id. Not secret, and still not something to commit.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The REST app's secret, from user-secrets, an environment variable or a vault. Never logged,
    /// never echoed in an error. Leaving it empty is how a deployment turns this adapter off.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The API root. Defaults to sandbox deliberately: a deployment that has not thought about which
    /// environment it is in should not be in the one that moves real money.
    /// </summary>
    public string ApiBase { get; set; } = SandboxApiBase;

    /// <summary>The transport's last-resort timeout. The platform's own policy sits under it.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// How many seconds before its stated expiry an access token is treated as spent.
    /// <para>
    /// A token that expires between being read from the cache and arriving at PayPal produces a 401
    /// on a payout, which the adapter cannot distinguish from a revoked credential. Retiring it
    /// early costs one extra token call an hour.
    /// </para>
    /// </summary>
    public int TokenRefreshSkewSeconds { get; set; } = 60;

    /// <summary>Whether enough has been supplied to make a call at all.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret)
        && !string.IsNullOrWhiteSpace(ApiBase);

    /// <summary>
    /// The webhook PayPal signs its payout callbacks against, from the developer dashboard.
    /// <para>
    /// <b>Required to verify anything, and its absence refuses rather than waves through.</b>
    /// PayPal does not sign with a shared secret the way Stripe does: verification is a call back to
    /// PayPal carrying this id alongside the transmission headers, and without the id there is no
    /// question to ask. An unverifiable callback about somebody being paid is exactly the one an
    /// attacker would send.
    /// </para>
    /// </summary>
    public string WebhookId { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is pointed at the sandbox. Read from the host because PayPal's credentials carry
    /// no marker of their own — see the type summary.
    /// </summary>
    public bool IsSandbox =>
        ApiBase.Contains("sandbox", StringComparison.OrdinalIgnoreCase);
}
