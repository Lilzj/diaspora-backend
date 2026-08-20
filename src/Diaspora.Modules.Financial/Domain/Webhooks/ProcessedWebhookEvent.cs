using System.Security.Cryptography;
using System.Text;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Webhooks;

/// <summary>
/// One provider callback, recorded once and never changed.
/// <para>
/// <b>This is the replay barrier, and it is a database constraint rather than a handler check.</b>
/// A provider that has not had a 2xx within its timeout redelivers, sometimes for days; a load
/// balancer retries; a deployment replays a queue. Every one of those arrives carrying the same
/// provider event identifier, and the unique index on
/// (<see cref="Provider"/>, <see cref="ProviderEventId"/>) is what makes the second one a lookup
/// rather than a second capture. A handler-level "have I seen this" is a race between two workers.
/// </para>
/// <para>
/// <b>A provider that sends no event identifier still gets replay protection.</b>
/// <see cref="DeriveEventId"/> hashes the raw body, so an identical redelivered payload collides
/// with itself. That is weaker than a real event id — two genuinely distinct callbacks with
/// byte-identical bodies would collapse — and it is strictly better than nothing, which is what the
/// alternative is.
/// </para>
/// <para>
/// <b>Append-only, like the ledger.</b> No public setter, no mutator, not
/// <see cref="ISoftDeletable"/> and not auditable — both of those exist to record that something
/// changed, and nothing about a received callback ever does.
/// </para>
/// </summary>
public sealed class ProcessedWebhookEvent : AggregateRoot<WebhookEventId>
{
    private ProcessedWebhookEvent(
        WebhookEventId id,
        string provider,
        string providerEventId,
        WebhookOutcome outcome,
        DateTimeOffset receivedAt) : base(id)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        Outcome = outcome;
        ReceivedAt = receivedAt;
    }

    private ProcessedWebhookEvent() { }

    /// <summary>Whose callback this was. Lower-cased, because a URL segment is not case-stable.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>
    /// The provider's own event identifier, or a hash of the body when it sent none. Unique with
    /// <see cref="Provider"/>.
    /// </summary>
    public string ProviderEventId { get; private set; } = string.Empty;

    public WebhookOutcome Outcome { get; private set; }

    /// <summary>What the adapter said happened. Null when nothing could be read.</summary>
    public string? EventKind { get; private set; }

    /// <summary>The provider's reference for the payment. The join key.</summary>
    public string? ExternalReference { get; private set; }

    /// <summary>The intent this was applied to, when one matched.</summary>
    public PaymentIntentId? PaymentIntentId { get; private set; }

    /// <summary>Why the platform refused it, when it did. Never the payload.</summary>
    public string? Detail { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public static ProcessedWebhookEvent Record(
        WebhookEventId id,
        string provider,
        string providerEventId,
        WebhookOutcome outcome,
        DateTimeOffset receivedAt,
        string? eventKind = null,
        string? externalReference = null,
        PaymentIntentId? paymentIntentId = null,
        string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);

        return new ProcessedWebhookEvent(
            id,
            NormalizeProvider(provider),
            providerEventId.Trim(),
            outcome,
            receivedAt)
        {
            EventKind = Trimmed(eventKind),
            ExternalReference = Trimmed(externalReference),
            PaymentIntentId = paymentIntentId,
            Detail = Trimmed(detail)
        };
    }

    /// <summary>A provider name as it is stored. Lower-cased and trimmed.</summary>
    public static string NormalizeProvider(string provider) =>
        provider.Trim().ToLowerInvariant();

    /// <summary>
    /// The identifier to store when the provider sent none: a hash of exactly the bytes that
    /// arrived. Never the body itself — a callback body carries whatever the provider chose to put
    /// in it, and this row is read by support.
    /// </summary>
    public static string DeriveEventId(string rawBody)
    {
        ArgumentNullException.ThrowIfNull(rawBody);

        return "body:" + Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
