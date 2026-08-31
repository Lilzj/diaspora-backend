namespace Diaspora.Payments.Flutterwave;

/// <summary>
/// What a deployment tells the Flutterwave adapter.
/// <para>
/// <b>The secret key is a secret and has no default.</b> It belongs in user-secrets, an environment
/// variable or a vault. An adapter without one is not registered, so the payout router refuses with
/// <c>financial.payout.provider_not_registered</c> rather than producing a 401 on a withdrawal.
/// </para>
/// <para>
/// <b>Test and live keys are distinguished by prefix</b> — <c>FLWSECK_TEST-</c> against
/// <c>FLWSECK-</c> — and <see cref="IsTestMode"/> reads it so a live suite can refuse to run
/// against production.
/// </para>
/// </summary>
public sealed class FlutterwaveOptions
{
    public const string SectionName = "Payments:Flutterwave";

    /// <summary>The name this adapter answers to, and the value the capability matrix must use.</summary>
    public string Provider { get; set; } = "flutterwave";

    /// <summary>
    /// The secret key. Never logged, never echoed in an error, never committed. Leaving it empty is
    /// how a deployment turns this adapter off.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>The API root. Overridable so a test can point at a stub, and for no other reason.</summary>
    public string ApiBase { get; set; } = "https://api.flutterwave.com";

    /// <summary>
    /// The currency the platform's own Flutterwave balance is denominated in, sent as
    /// <c>debit_currency</c>. Empty leaves it out and lets the account default apply.
    /// <para>
    /// Configurable rather than derived: a platform settling NGN out of a USD balance and one
    /// settling it out of an NGN balance send the same payout instruction, and only the deployment
    /// knows which it is.
    /// </para>
    /// </summary>
    public string DebitCurrency { get; set; } = string.Empty;

    /// <summary>The transport's last-resort timeout. The platform's own policy sits under it.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// The secret hash Flutterwave echoes in the <c>verif-hash</c> header, set on the dashboard.
    /// <para>
    /// <b>Without it, callbacks are refused rather than trusted.</b> This is a shared secret rather
    /// than a signature over the body — see <c>FlutterwavePayoutWebhookProcessor</c> for what that
    /// does and does not prove — so it must be treated as a credential and never committed.
    /// </para>
    /// </summary>
    public string WebhookSecretHash { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(ApiBase);

    /// <summary>Whether the configured key is a test key. Read from the prefix — see the type summary.</summary>
    public bool IsTestMode =>
        SecretKey.StartsWith("FLWSECK_TEST", StringComparison.OrdinalIgnoreCase);
}
