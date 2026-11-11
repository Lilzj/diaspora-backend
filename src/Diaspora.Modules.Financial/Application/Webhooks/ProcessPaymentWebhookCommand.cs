using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Webhooks;

/// <summary>
/// A provider's callback, as it arrived.
/// <para>
/// <b>Carries no <c>[Authorize]</c>, and that is not an oversight.</b> A provider has no bearer
/// token; the signature over the raw body <em>is</em> the authentication, and it is checked by the
/// registered processor before a single field of the payload is read. Adding a permission here would
/// be security theatre over the wrong credential — and would break every real integration.
/// </para>
/// <para>
/// <b>Transactional, because one callback settles several things.</b> Capturing an intent posts to
/// the ledger, completes a purchase, activates a placement, credits a deposit and records the
/// callback itself. All of those land together or the provider redelivers and they are all tried
/// again.
/// </para>
/// </summary>
[Transactional(Reason = "One callback captures a payment, settles what it bought and records itself.")]
[AllowAnonymous(
    "A provider callback. It carries no bearer token and never will — the request signature is " +
    "the credential, and the handler verifies it before reading anything.")]
public sealed record ProcessPaymentWebhookCommand : ICommand<WebhookAcknowledgementResponse>
{
    /// <summary>Whose callback this is, from the URL. Matched against registered processors.</summary>
    public required string Provider { get; init; }

    /// <summary>Exactly the bytes that arrived, as text. Never re-serialised — see the envelope.</summary>
    public required string RawBody { get; init; }

    /// <summary>
    /// The request headers, where the signature lives.
    /// <para>
    /// Passed through verbatim rather than picked apart here: which header carries the signature is
    /// the provider's decision and therefore the adapter's knowledge.
    /// </para>
    /// </summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
