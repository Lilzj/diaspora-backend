namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// What a provider's payout callback turned out to mean, in platform terms.
/// <para>
/// <b>The adapter translates; the platform decides.</b> An adapter says "the transfer carrying this
/// reference was paid"; whether a withdrawal in its current state may become <c>Completed</c> is the
/// aggregate's decision, made afterwards. That separation is what stops a compromised or buggy
/// provider driving a withdrawal somewhere the state machine forbids.
/// </para>
/// <para>
/// <b>The reference is the platform's, not the provider's.</b> Every adapter sends the withdrawal's
/// own identifier out with the instruction — Stripe as <c>transfer_group</c>, Flutterwave as
/// <c>reference</c>, PayPal as <c>sender_batch_id</c> — precisely so a callback can be matched
/// without depending on an id the platform may never have received.
/// </para>
/// </summary>
public sealed record PayoutWebhookResult
{
    /// <summary>False when the signature did not verify. Nothing downstream runs.</summary>
    public required bool IsVerified { get; init; }

    /// <summary>False when the payload is understood but is about nothing the platform tracks.</summary>
    public required bool IsRecognized { get; init; }

    /// <summary>
    /// The withdrawal this is about — the platform's own reference, sent out with the instruction.
    /// </summary>
    public string? WithdrawalReference { get; init; }

    /// <summary>
    /// What happened. <c>paid</c>, <c>failed</c> or <c>reversed</c>. A closed vocabulary the adapter
    /// maps onto, deliberately smaller than any provider's own: a payout either left, did not, or
    /// came back, and every provider-specific stage in between is one of those three to a member.
    /// </summary>
    public string? EventKind { get; init; }

    /// <summary>The provider's own handle for the movement, when it sent one.</summary>
    public string? ProviderReference { get; init; }

    public string? FailureCode { get; init; }

    public string? FailureMessage { get; init; }

    /// <summary>The provider's own event identifier. The idempotency key for redelivery.</summary>
    public string? EventId { get; init; }

    public static PayoutWebhookResult Unverified() =>
        new() { IsVerified = false, IsRecognized = false };

    public static PayoutWebhookResult Ignored(string? eventId = null) =>
        new() { IsVerified = true, IsRecognized = false, EventId = eventId };
}

/// <summary>
/// Turns a provider's payout callback into something the platform can act on.
/// <para>
/// <b>Separate from <c>IPaymentWebhookProcessor</c>, and on its own route.</b> Payout callbacks are
/// delivered to <c>/webhooks/{provider}/payouts</c> rather than shared with the payment path, which
/// every provider here supports by letting an endpoint select the events it receives. One route
/// carrying both would mean each payload being offered to two processors and verified twice, and
/// would make "which adapter owns this body" a guess rather than a routing decision.
/// </para>
/// <para>
/// <b>Verification is the processor's job and it must never be optional.</b> An unverified callback
/// is an unauthenticated request that moves money; the handler refuses anything whose
/// <see cref="PayoutWebhookResult.IsVerified"/> is false, without reading the rest.
/// </para>
/// <para>
/// <b>A callback is not the only way an outcome arrives, and must not be the only way it can.</b>
/// <c>ISettlementStatusProvider</c> resolves a withdrawal by asking; this resolves one by being
/// told. A deployment whose webhook endpoint is unreachable for a day still settles, a little later,
/// through the sweep — which is why neither path is allowed to be the sole route to a completion.
/// </para>
/// </summary>
public interface IPayoutWebhookProcessor
{
    /// <summary>Which provider's payout callbacks this processes.</summary>
    string Provider { get; }

    Task<PayoutWebhookResult> ProcessAsync(
        PayoutWebhookEnvelope envelope, CancellationToken cancellationToken = default);
}

/// <summary>
/// A payout callback as it arrived, before anybody has believed a word of it.
/// <para>
/// The raw body is kept as text with the headers beside it, for the reason
/// <c>PaymentWebhookEnvelope</c> gives: verifying a signature over a re-serialised object is
/// verifying a signature over something the provider never sent.
/// </para>
/// </summary>
public sealed record PayoutWebhookEnvelope
{
    public required string Provider { get; init; }

    /// <summary>Exactly the bytes that arrived, as text. Never re-serialised.</summary>
    public required string RawBody { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }
}
