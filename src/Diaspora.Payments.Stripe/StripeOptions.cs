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
    /// How far from now a callback's timestamp may be, in seconds.
    /// <para>
    /// A correctly signed callback captured off the wire stays correctly signed for ever, so this is
    /// not a nicety — it is the half of the scheme that makes the signature mean anything about
    /// freshness. Five minutes is Stripe's own recommendation and leaves room for clock drift.
    /// </para>
    /// </summary>
    public int WebhookToleranceSeconds { get; set; } = 300;

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
