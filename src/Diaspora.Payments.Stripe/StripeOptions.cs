namespace Diaspora.Payments.Stripe;

/// <summary>
/// What a deployment tells the Stripe adapter.
/// <para>
/// <b>The secret key is a secret and never has a default.</b> It belongs in user-secrets, an
/// environment variable or a vault — never in <c>appsettings.json</c>, and never in a repository.
/// An adapter with no key is not registered at all, so a deployment that has not supplied one gets
/// a routing refusal naming the missing provider rather than a 401 on somebody's withdrawal.
/// </para>
/// <para>
/// <b>Test and live keys are the same shape and must never be mixed.</b> Stripe distinguishes them
/// only by the <c>sk_test_</c> / <c>sk_live_</c> prefix, so <see cref="IsTestMode"/> reads it and
/// the registration logs which one a host started with. A deployment that believed it was in
/// sandbox and was not would find out by moving somebody's money.
/// </para>
/// </summary>
public sealed class StripeOptions
{
    public const string SectionName = "Payments:Stripe";

    /// <summary>
    /// The name this adapter answers to, and the value the capability matrix must use. Configurable
    /// because the matrix is configuration: a deployment running two Stripe accounts — a platform
    /// and a subsidiary — registers the adapter twice under two names.
    /// </summary>
    public string Provider { get; set; } = "stripe";

    /// <summary>
    /// The Stripe secret key, from user-secrets, an environment variable or a vault. Never logged,
    /// never echoed in an error, and never written to configuration that is checked in. Leaving it
    /// empty is how a deployment turns this adapter off.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// The API root. Overridable so a test can point the adapter at a stub without a network, and
    /// for no other reason — there is no second Stripe.
    /// </summary>
    public string ApiBase { get; set; } = "https://api.stripe.com";

    /// <summary>
    /// The Stripe API version this adapter was written against, sent on every request.
    /// <para>
    /// Pinned deliberately. Stripe rolls accounts forward on their own schedule, and an adapter that
    /// sent no version would change behaviour on a date nobody chose, in the one subsystem where a
    /// surprise moves money. Raising it is a code change with a test run behind it.
    /// </para>
    /// </summary>
    public string ApiVersion { get; set; } = "2025-08-27.basil";

    /// <summary>The transport's last-resort timeout. The platform's own policy sits under it.</summary>
    public int TimeoutSeconds { get; set; } = 20;

    /// <summary>
    /// The endpoint signing secret, <c>whsec_…</c>. A different secret from
    /// <see cref="SecretKey"/> and issued per endpoint.
    /// <para>
    /// <b>Without it, callbacks are refused rather than trusted.</b> A deployment that has not
    /// configured this has no way to tell a Stripe callback from anybody's POST, and treating
    /// "we cannot check" as "it is fine" would make the webhook route an unauthenticated way to
    /// complete somebody's payout.
    /// </para>
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>
    /// Additional endpoint signing secrets, tried alongside <see cref="WebhookSecret"/>.
    /// <para>
    /// <b>Stripe issues a different secret per endpoint, and this integration registers two.</b>
    /// Payment callbacks and payout callbacks arrive on separate routes — deliberately, so an
    /// endpoint can be scoped to the events it owns — which means two Stripe endpoints and two
    /// <c>whsec_</c> values. A verifier that knew only one of them would refuse every callback from
    /// the other, for ever, as an invalid signature. A refused payment callback is a deposit that
    /// completed at Stripe and never moved the member's balance.
    /// </para>
    /// <para>
    /// <b>It also covers a secret roll.</b> Stripe keeps the previous secret live for up to 24
    /// hours and signs with both, so a deployment mid-rotation legitimately has two.
    /// </para>
    /// <para>
    /// Set as <c>Payments__Stripe__WebhookSecrets__0</c>, <c>__1</c>, and so on.
    /// </para>
    /// </summary>
    public IList<string> WebhookSecrets { get; set; } = [];

    /// <summary>
    /// Every secret a callback may be signed with. Order is irrelevant; each is tried.
    /// </summary>
    public IReadOnlyList<string> AcceptedWebhookSecrets =>
    [
        .. new[] { WebhookSecret }
            .Concat(WebhookSecrets ?? [])
            .Where(secret => !string.IsNullOrWhiteSpace(secret))
            .Select(secret => secret.Trim())
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>Whether any callback can be verified at all.</summary>
    public bool HasWebhookSecret => AcceptedWebhookSecrets.Count > 0;

    /// <summary>
    /// How far from now a callback's timestamp may be, in seconds.
    /// <para>
    /// A correctly signed callback captured off the wire stays correctly signed for ever, so this is
    /// not a nicety — it is the half of the scheme that makes the signature mean anything about
    /// freshness. Five minutes is Stripe's own recommendation and leaves room for clock drift.
    /// </para>
    /// </summary>
    public int WebhookToleranceSeconds { get; set; } = 300;

    /// <summary>
    /// Where Stripe returns a member after a completed Checkout, and after a cancelled one.
    /// <para>
    /// <b>Checkout, rather than a bare client secret, because the app has no payment SDK.</b> A
    /// PaymentIntent's client secret has to be confirmed by Stripe's own SDK, and there is none in
    /// the client; what the client <em>does</em> have is a challenge renderer that opens an HTTPS
    /// URL. A hosted Checkout page is that URL, and it keeps the card form out of the app entirely.
    /// </para>
    /// <para>
    /// <b>Stripe requires http(s) here — a custom scheme is rejected</b>, so these cannot be
    /// <c>diaspora://</c> deep links. Point them at a web page that deep-links onward.
    /// </para>
    /// </summary>
    public string CheckoutSuccessUrl { get; set; } = string.Empty;

    /// <inheritdoc cref="CheckoutSuccessUrl"/>
    public string CheckoutCancelUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether a hosted Checkout can be started. Separate from <see cref="IsConfigured"/> on
    /// purpose: a deployment may hold a key for payouts alone, and a missing return URL must refuse
    /// the charge rather than fail the whole adapter's registration.
    /// </summary>
    public bool IsCheckoutConfigured =>
        IsConfigured
        && !string.IsNullOrWhiteSpace(CheckoutSuccessUrl)
        && !string.IsNullOrWhiteSpace(CheckoutCancelUrl);

    /// <summary>Whether enough has been supplied to make a call at all.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey) && !string.IsNullOrWhiteSpace(ApiBase);

    /// <summary>
    /// Whether the configured key is a test key. Read from the prefix because that is the only
    /// thing that distinguishes them — see the type summary.
    /// </summary>
    public bool IsTestMode =>
        SecretKey.StartsWith("sk_test_", StringComparison.Ordinal)
        || SecretKey.StartsWith("rk_test_", StringComparison.Ordinal);
}
