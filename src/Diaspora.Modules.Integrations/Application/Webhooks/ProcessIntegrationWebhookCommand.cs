using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Integrations.V1.Events;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Integrations.Domain.Connections;
using Diaspora.Modules.Integrations.Domain.Enums;
using Diaspora.Modules.Integrations.Domain.Errors;
using Diaspora.Modules.Integrations.Domain.Identifiers;
using Diaspora.Modules.Integrations.Domain.Providers;
using Diaspora.Modules.Integrations.Domain.Repositories;
using Diaspora.Modules.Integrations.Domain.Webhooks;
using Diaspora.Modules.Integrations.Extensibility.Providers;
using Diaspora.Modules.Integrations.Infrastructure.Providers;
using Diaspora.Modules.Integrations.Observability;
using Diaspora.Platform.Messaging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Integrations.Application.Webhooks;

/// <summary>What the platform tells a provider about a callback.</summary>
public sealed record IntegrationWebhookAcknowledgement
{
    public required string Outcome { get; init; }

    /// <summary>True when this exact callback had already been recorded.</summary>
    public required bool WasDuplicate { get; init; }

    /// <summary>Why, when it was refused or ignored. Never the payload.</summary>
    public string? Detail { get; init; }
}

/// <summary>
/// A provider callback.
/// <para>
/// The raw body travels as text and unaltered, because a signature verified over a re-serialised
/// object is a signature verified over something the provider never sent.
/// </para>
/// </summary>
[Transactional(Reason =
    "Records the callback, applies it to the connection and enqueues the contract that tells the "
    + "member. Committing the record without the effect would silently swallow a revocation; "
    + "committing the effect without the record would let the next redelivery apply it again.")]
public sealed record ProcessIntegrationWebhookCommand : ICommand<IntegrationWebhookAcknowledgement>
{
    public required string Provider { get; init; }

    public required string RawBody { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }
}

/// <summary>
/// Turns a provider callback into a state change, or refuses it.
/// <para>
/// <b>Five gates, in this order, and none is skippable.</b> A registered verifier for the named
/// provider; a verifier that actually has its secret; a signature that verifies over the raw body;
/// a payload that is not a replay of one outside the acceptable window; and an event identifier not
/// seen before. Only then is anything acted on.
/// </para>
/// <para>
/// <b>What is recorded and what is not is a deliberate line.</b> A callback that fails the first
/// three gates writes no row — anybody can post an unsigned body, and a table anybody can write a
/// row to is a table anybody can fill. A callback that passes them and is then unmatched, ignored or
/// refused <em>is</em> recorded, because "we received something we could not act on" and "we
/// received nothing" are different answers to why a member's connection still says connected.
/// </para>
/// <para>
/// <b>Every status code here is chosen.</b> 404 for a provider with no verifier. 400 for a signature
/// that did not verify — loud, because that is either a rotated secret nobody updated or somebody
/// probing. 200 for everything else, <em>including</em> a callback about a connection this
/// deployment has never heard of: answering 4xx there buys an unbounded redelivery loop against a
/// reference that will never resolve.
/// </para>
/// </summary>
public sealed class ProcessIntegrationWebhookCommandHandler
    : ICommandHandler<ProcessIntegrationWebhookCommand, IntegrationWebhookAcknowledgement>
{
    private readonly IntegrationProviderRegistry _registry;
    private readonly IProcessedIntegrationWebhookRepository _received;
    private readonly IIntegrationConnectionRepository _connections;
    private readonly IIntegrationEventPublisher _events;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<ProcessIntegrationWebhookCommandHandler> _logger;

    public ProcessIntegrationWebhookCommandHandler(
        IntegrationProviderRegistry registry,
        IProcessedIntegrationWebhookRepository received,
        IIntegrationConnectionRepository connections,
        IIntegrationEventPublisher events,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock,
        ILogger<ProcessIntegrationWebhookCommandHandler> logger)
    {
        _registry = registry;
        _received = received;
        _connections = connections;
        _events = events;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<IntegrationWebhookAcknowledgement>> Handle(
        ProcessIntegrationWebhookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = ProcessedIntegrationWebhook.NormalizeProvider(command.Provider);

        // Gate one. No verifier means no way to establish authenticity, and "nobody registered a
        // verifier" must never become "no verification was needed".
        if (_registry.FindVerifier(provider) is not { } verifier)
        {
            IntegrationsTelemetry.WebhookFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("reason", "provider_unknown"));

            return Result.Failure<IntegrationWebhookAcknowledgement>(
                IntegrationErrors.WebhookProviderUnknown());
        }

        // Gate two. A verifier without its secret cannot verify, so it must not accept.
        if (!await verifier.IsConfiguredAsync(cancellationToken))
        {
            IntegrationsTelemetry.WebhookFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("reason", "secret_missing"));

            _logger.LogError(
                "A {Provider} callback arrived and the verifier has no secret configured. Callbacks "
                + "from this provider are being refused until it is.",
                provider);

            return Result.Failure<IntegrationWebhookAcknowledgement>(
                IntegrationErrors.WebhookSignatureInvalid());
        }

        var verification = await verifier.VerifyAsync(
            new IntegrationWebhookEnvelope(provider, command.RawBody, command.Headers, _clock.UtcNow),
            cancellationToken);

        // Gate three. Loud, and no row: see the class summary.
        if (!verification.IsVerified)
        {
            IntegrationsTelemetry.WebhookFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("reason", "signature_invalid"));

            _logger.LogWarning(
                "Rejected a {Provider} callback whose signature did not verify.", provider);

            return Result.Failure<IntegrationWebhookAcknowledgement>(
                IntegrationErrors.WebhookSignatureInvalid());
        }

        // Gate four. A correctly-signed replay from outside the window is a different finding from
        // a bad signature: somebody captured a real callback. It is refused and it is recorded,
        // because the record is the evidence.
        if (verification.IsStale)
        {
            IntegrationsTelemetry.WebhookFailures.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("reason", "replayed"));

            _logger.LogWarning(
                "A {Provider} callback verified but its timestamp is outside the replay window. It "
                + "was refused.",
                provider);

            return await RecordAsync(
                provider,
                EventId(verification, command.RawBody),
                IntegrationWebhookOutcome.Refused,
                verification,
                connection: null,
                "The callback's timestamp is outside the acceptable replay window.",
                cancellationToken);
        }

        var eventId = EventId(verification, command.RawBody);

        // Gate five. The replay barrier proper — a unique index, not a check. See the aggregate.
        if (await _received.FindAsync(provider, eventId, cancellationToken) is { } already)
        {
            IntegrationsTelemetry.WebhooksReceived.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("outcome", "duplicate"));

            return Result.Success(new IntegrationWebhookAcknowledgement
            {
                Outcome = already.Outcome.ToString(),
                WasDuplicate = true,
                Detail = already.Detail
            });
        }

        return await ApplyAsync(provider, eventId, verification, cancellationToken);
    }

    private async Task<Result<IntegrationWebhookAcknowledgement>> ApplyAsync(
        string provider,
        string eventId,
        IntegrationWebhookVerification verification,
        CancellationToken cancellationToken)
    {
        // §14: "do not process arbitrary webhook payloads merely because they contain a familiar
        // event name." A kind outside the closed set is recorded and inert.
        if (!IntegrationWebhookEventKinds.IsKnown(verification.EventKind))
        {
            return await RecordAsync(
                provider,
                eventId,
                IntegrationWebhookOutcome.Ignored,
                verification,
                connection: null,
                "Not an event kind this platform acts on.",
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(verification.ExternalReference))
        {
            return await RecordAsync(
                provider,
                eventId,
                IntegrationWebhookOutcome.Unmatched,
                verification,
                connection: null,
                "The callback named no account this platform could resolve.",
                cancellationToken);
        }

        var connection = await _connections.FindByExternalAccountAsync(
            provider, verification.ExternalReference, cancellationToken);

        if (connection is null)
        {
            // A 200 with an Unmatched record. A 4xx here buys an unbounded redelivery loop against
            // a reference that will never resolve.
            return await RecordAsync(
                provider,
                eventId,
                IntegrationWebhookOutcome.Unmatched,
                verification,
                connection: null,
                "No connection matched that account.",
                cancellationToken);
        }

        var now = _clock.UtcNow;

        switch (verification.EventKind)
        {
            case IntegrationWebhookEventKinds.GrantRevoked:
                return await ApplyRevocationAsync(
                    provider, eventId, verification, connection, now, cancellationToken);

            case IntegrationWebhookEventKinds.GrantChanged:
                // A changed grant is not a revoked one. It is recorded and the connection is left
                // usable: the next real call is what discovers whether the narrowed scope still
                // covers what the member asked for, and guessing here would disconnect somebody who
                // merely toggled an unrelated permission.
                return await RecordAsync(
                    provider,
                    eventId,
                    IntegrationWebhookOutcome.Ignored,
                    verification,
                    connection,
                    "The grant changed. The next call will establish whether it still suffices.",
                    cancellationToken);

            case IntegrationWebhookEventKinds.CalendarChanged:
            case IntegrationWebhookEventKinds.WatchExpiring:
                // Recorded and inert, honestly. Acting on either needs the watch-channel half that
                // this deployment does not have — a registered channel to renew, or a sync to
                // trigger — and pretending otherwise would be a success this platform did not
                // produce. The capability report says the same thing.
                return await RecordAsync(
                    provider,
                    eventId,
                    IntegrationWebhookOutcome.Ignored,
                    verification,
                    connection,
                    "This deployment has no registered watch channel, so there is nothing to act on.",
                    cancellationToken);

            default:
                return await RecordAsync(
                    provider,
                    eventId,
                    IntegrationWebhookOutcome.Ignored,
                    verification,
                    connection,
                    "Not an event kind this platform acts on.",
                    cancellationToken);
        }
    }

    /// <summary>
    /// The one callback that changes something.
    /// <para>
    /// A member removed Diaspora from their provider account. This is what the register asks the
    /// webhook infrastructure to exist for: it is what stops the connection centre "showing
    /// <c>CONNECTED</c> for a grant removed at Google a week ago".
    /// </para>
    /// </summary>
    private async Task<Result<IntegrationWebhookAcknowledgement>> ApplyRevocationAsync(
        string provider,
        string eventId,
        IntegrationWebhookVerification verification,
        IntegrationConnection connection,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (connection.Status is IntegrationConnectionStatus.Disconnected
            or IntegrationConnectionStatus.ReauthorizationRequired)
        {
            return await RecordAsync(
                provider,
                eventId,
                IntegrationWebhookOutcome.Ignored,
                verification,
                connection,
                "The connection was already in a state that needs the member.",
                cancellationToken);
        }

        connection.RequireReauthorization(
            "This connection was removed at the provider. Connect it again to carry on.", now);

        _connections.Update(connection);

        if (!ProviderKey.TryParse(connection.Provider, out var key))
        {
            key = default;
        }

        await _events.PublishAsync(
            new IntegrationReauthorizationRequiredIntegrationEvent
            {
                UserId = connection.OwnerId,
                Provider = connection.Provider,
                ProviderDomain = key.Domain ?? connection.ProviderDomain,
                Reason = "revoked",
                OccurredAtUtc = now
            },
            cancellationToken);

        IntegrationsTelemetry.ReauthorizationsRequired.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("cause", "provider_webhook"));

        return await RecordAsync(
            provider,
            eventId,
            IntegrationWebhookOutcome.Applied,
            verification,
            connection,
            detail: null,
            cancellationToken);
    }

    private async Task<Result<IntegrationWebhookAcknowledgement>> RecordAsync(
        string provider,
        string eventId,
        IntegrationWebhookOutcome outcome,
        IntegrationWebhookVerification verification,
        IntegrationConnection? connection,
        string? detail,
        CancellationToken cancellationToken)
    {
        _received.Add(ProcessedIntegrationWebhook.Record(
            IntegrationWebhookEventId.From(_guids.NewSequentialGuid()),
            provider,
            eventId,
            outcome,
            _clock.UtcNow,
            verification.EventKind,
            verification.ExternalReference,
            connection?.Id,
            detail));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        IntegrationsTelemetry.WebhooksReceived.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("outcome", outcome.ToString()));

        return Result.Success(new IntegrationWebhookAcknowledgement
        {
            Outcome = outcome.ToString(),
            WasDuplicate = false,
            Detail = detail
        });
    }

    /// <summary>
    /// The replay key.
    /// <para>
    /// The provider's own identifier where there is one; a hash of the exact bytes otherwise. The
    /// fallback is weaker — two genuinely distinct callbacks with byte-identical bodies would
    /// collapse — and strictly better than no replay protection, which is the alternative.
    /// </para>
    /// </summary>
    private static string EventId(IntegrationWebhookVerification verification, string rawBody) =>
        string.IsNullOrWhiteSpace(verification.EventId)
            ? ProcessedIntegrationWebhook.DeriveEventId(rawBody)
            : verification.EventId.Trim();
}
