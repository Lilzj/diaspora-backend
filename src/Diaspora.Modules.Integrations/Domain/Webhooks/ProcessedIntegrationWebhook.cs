using System.Security.Cryptography;
using System.Text;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Integrations.Domain.Webhooks;

/// <summary>
/// One provider callback, recorded once and never changed.
/// <para>
/// <b>The replay barrier, and it is a database constraint rather than a handler check.</b> The
/// unique index on (<see cref="Provider"/>, <see cref="ProviderEventId"/>) is what makes a
/// redelivery a lookup instead of a second effect. A handler-level "have I seen this" is a race
/// between two workers, and providers redeliver for days.
/// </para>
/// <para>
/// <b>This is a second table and not a second system.</b> B15 owns
/// <c>ProcessedWebhookEvent</c> for payment callbacks and that stays exactly where it is: a payment
/// callback resolves to a payment intent and is B15's to interpret. This one resolves to an
/// integration connection and is this module's. Sharing one table would mean either module could
/// mark the other's callbacks processed, and a schema-crossing write is precisely what the module
/// boundary exists to prevent. The <em>shape</em> is deliberately identical, because the problem is.
/// </para>
/// <para>
/// <b>The payload is never stored.</b> A calendar watch notification names a calendar, and a
/// calendar's title is personal — "Therapy", "Dad's chemo". What is kept is the provider, the event
/// identifier, the kind, the resolved connection and the outcome. §42's retention question has a
/// short answer when there is nothing sensitive to retain.
/// </para>
/// <para>
/// Append-only. No mutator, not soft-deletable and not auditable — both of those exist to record
/// that something changed, and nothing about a received callback ever does.
/// </para>
/// </summary>
public sealed class ProcessedIntegrationWebhook : AggregateRoot<IntegrationWebhookEventId>
{
    public const int ProviderLength = 64;

    /// <summary>A provider event id, or <c>body:</c> plus a hex SHA-256.</summary>
    public const int ProviderEventIdLength = 200;

    public const int EventKindLength = 64;

    /// <summary>The provider's own resource identifier — a channel, a watch, an account.</summary>
    public const int ExternalReferenceLength = 256;

    /// <summary>Why the platform refused it, when it did. Never the payload.</summary>
    public const int DetailLength = 500;

    private ProcessedIntegrationWebhook(
        IntegrationWebhookEventId id,
        string provider,
        string providerEventId,
        IntegrationWebhookOutcome outcome,
        DateTimeOffset receivedAt) : base(id)
    {
        Provider = provider;
        ProviderEventId = providerEventId;
        Outcome = outcome;
        ReceivedAt = receivedAt;
    }

    private ProcessedIntegrationWebhook() { }

    /// <summary>Whose callback. Lower-cased, because a URL segment is not case-stable.</summary>
    public string Provider { get; private set; } = string.Empty;

    /// <summary>The provider's identifier, or a hash of the body when it sent none.</summary>
    public string ProviderEventId { get; private set; } = string.Empty;

    public IntegrationWebhookOutcome Outcome { get; private set; }

    /// <summary>What the adapter said happened. Null when nothing could be read.</summary>
    public string? EventKind { get; private set; }

    /// <summary>The provider's reference. The join key onto a connection.</summary>
    public string? ExternalReference { get; private set; }

    /// <summary>The connection this was applied to, when one matched.</summary>
    public IntegrationConnectionId? ConnectionId { get; private set; }

    public string? Detail { get; private set; }

    public DateTimeOffset ReceivedAt { get; private set; }

    public static ProcessedIntegrationWebhook Record(
        IntegrationWebhookEventId id,
        string provider,
        string providerEventId,
        IntegrationWebhookOutcome outcome,
        DateTimeOffset receivedAt,
        string? eventKind = null,
        string? externalReference = null,
        IntegrationConnectionId? connectionId = null,
        string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);

        return new ProcessedIntegrationWebhook(
            id,
            NormalizeProvider(provider),
            Clip(providerEventId, ProviderEventIdLength)!,
            outcome,
            receivedAt)
        {
            EventKind = Clip(eventKind, EventKindLength),
            ExternalReference = Clip(externalReference, ExternalReferenceLength),
            ConnectionId = connectionId,
            Detail = Clip(detail, DetailLength)
        };
    }

    /// <summary>A provider name as it is stored.</summary>
    public static string NormalizeProvider(string provider) => provider.Trim().ToLowerInvariant();

    /// <summary>
    /// The identifier to store when the provider sent none: a hash of exactly the bytes that
    /// arrived.
    /// <para>
    /// Weaker than a real event id — two genuinely distinct callbacks with byte-identical bodies
    /// would collapse — and strictly better than nothing, which is the alternative. Never the body
    /// itself: a callback carries whatever the provider chose to put in it, and this row is read by
    /// support.
    /// </para>
    /// </summary>
    public static string DeriveEventId(string rawBody)
    {
        ArgumentNullException.ThrowIfNull(rawBody);

        return "body:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody)));
    }

    private static string? Clip(string? value, int length) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim().Length <= length ? value.Trim() : value.Trim()[..length];
}
