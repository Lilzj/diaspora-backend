using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Webhooks;

/// <summary>
/// A payout provider's callback, as it arrived.
/// <para>
/// <b>Carries no <c>[Authorize]</c>, for the reason <see cref="ProcessPaymentWebhookCommand"/>
/// gives:</b> the signature over the raw body is the authentication, and it is checked before a
/// field of the payload is read.
/// </para>
/// <para>
/// <b>Transactional, because one callback settles a withdrawal and posts the ledger entries behind
/// it.</b> Completing a payout turns a hold into money that has left; both land together or the
/// provider redelivers and both are tried again.
/// </para>
/// </summary>
[Transactional(Reason = "One callback completes a payout, posts its ledger entries and records itself.")]
public sealed record ProcessPayoutWebhookCommand : ICommand<WebhookAcknowledgementResponse>
{
    /// <summary>Whose callback this is, from the URL. Matched against registered processors.</summary>
    public required string Provider { get; init; }

    /// <summary>Exactly the bytes that arrived, as text. Never re-serialised — see the envelope.</summary>
    public required string RawBody { get; init; }

    /// <summary>The request headers, where the signature lives.</summary>
    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}
