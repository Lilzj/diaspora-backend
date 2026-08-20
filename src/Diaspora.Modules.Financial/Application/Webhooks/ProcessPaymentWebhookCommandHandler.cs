using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Webhooks;
using Diaspora.Modules.Financial.Extensibility.Gateways;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Webhooks;

/// <summary>
/// Turns a provider's callback into a state change, or refuses it.
/// <para>
/// <b>Four gates, in this order, and none of them is skippable.</b> A registered processor for the
/// named provider; a signature that verifies over the raw body; an event identifier not seen before;
/// and a transition the aggregate itself allows. A callback that fails any of them is <em>recorded</em>
/// and does nothing — because "we received something we could not verify" and "we received nothing"
/// are the two most different answers to why a member was not charged, and a system that stores only
/// what it acted on cannot tell them apart.
/// </para>
/// <para>
/// <b>The provider says what happened; the platform decides what that means.</b> The adapter
/// translates a payload into an outcome and a reference. Whether that outcome is legal for the intent
/// in question is the state machine's decision, made afterwards — which is what stops a compromised
/// or buggy provider driving an intent somewhere the platform forbids. A "captured" callback for an
/// already-captured intent is refused by <c>PaymentIntent.Capture</c>, and the ledger's reference
/// idempotency refuses the posting behind it as well. Two independent barriers, because this is the
/// path where a duplicate costs real money.
/// </para>
/// <para>
/// <b>An unmatched or unrecognised callback is a success, not an error.</b> Answering 4xx to a
/// provider for a payment this deployment does not know about buys an unbounded redelivery loop
/// against a reference that will never resolve. The refusal that <em>is</em> reported as a failure is
/// the signature — because a signature that does not verify should be loud.
/// </para>
/// </summary>
public sealed class ProcessPaymentWebhookCommandHandler
    : ICommandHandler<ProcessPaymentWebhookCommand, WebhookAcknowledgementResponse>
{
    private readonly IEnumerable<IPaymentWebhookProcessor> _processors;
    private readonly IProcessedWebhookEventRepository _events;
    private readonly IPaymentIntentRepository _intents;
    private readonly PaymentProcessor _payments;
    private readonly CurrencyCatalog _currencies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<ProcessPaymentWebhookCommandHandler> _logger;

    public ProcessPaymentWebhookCommandHandler(
        IEnumerable<IPaymentWebhookProcessor> processors,
        IProcessedWebhookEventRepository events,
        IPaymentIntentRepository intents,
        PaymentProcessor payments,
        CurrencyCatalog currencies,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock,
        ILogger<ProcessPaymentWebhookCommandHandler> logger)
    {
        _processors = processors;
        _events = events;
        _intents = intents;
        _payments = payments;
        _currencies = currencies;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<WebhookAcknowledgementResponse>> Handle(
        ProcessPaymentWebhookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = ProcessedWebhookEvent.NormalizeProvider(command.Provider);
        var processor = _processors.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase));

        if (processor is null)
        {
            // Not recorded. There is nothing to de-duplicate against and no signature to have
            // verified, so a row here would be an unauthenticated caller writing to the database.
            FinancialTelemetry.WebhooksReceived.Add(
                1, new KeyValuePair<string, object?>("outcome", "provider_unknown"));

            return Result.Failure<WebhookAcknowledgementResponse>(Error.NotFound(
                FinancialErrorCodes.WebhookProviderUnknown,
                "No callback processor is registered for that provider."));
        }

        var result = await processor.ProcessAsync(
            new PaymentWebhookEnvelope
            {
                Provider = provider,
                RawBody = command.RawBody,
                Headers = command.Headers,
                ReceivedAt = _clock.UtcNow
            },
            cancellationToken);

        if (!result.IsVerified)
        {
            // Also not recorded, and for the same reason: anybody can post an unsigned body, and a
            // table anybody can write a row to is a table anybody can fill. The counter is the
            // signal, and it is the one worth alerting on.
            FinancialTelemetry.WebhooksReceived.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("outcome", "unverified"));

            _logger.LogWarning(
                "Rejected a {Provider} callback whose signature did not verify.", provider);

            return Result.Failure<WebhookAcknowledgementResponse>(Error.Validation(
                FinancialErrorCodes.WebhookSignatureInvalid,
                "That callback's signature did not verify."));
        }

        var eventId = string.IsNullOrWhiteSpace(result.EventId)
            ? ProcessedWebhookEvent.DeriveEventId(command.RawBody)
            : result.EventId.Trim();

        if (await _events.FindAsync(provider, eventId, cancellationToken) is { } already)
        {
            FinancialTelemetry.WebhooksReceived.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("outcome", "duplicate"));

            return Result.Success(new WebhookAcknowledgementResponse
            {
                Outcome = already.Outcome.ToString(),
                WasDuplicate = true,
                Detail = already.Detail
            });
        }

        var applied = await ApplyAsync(provider, eventId, result, cancellationToken);

        _events.Add(applied.Record);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        FinancialTelemetry.WebhooksReceived.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("outcome", applied.Record.Outcome.ToString()));

        return Result.Success(new WebhookAcknowledgementResponse
        {
            Outcome = applied.Record.Outcome.ToString(),
            WasDuplicate = false,
            Detail = applied.Record.Detail
        });
    }

    private async Task<(ProcessedWebhookEvent Record, bool Changed)> ApplyAsync(
        string provider,
        string eventId,
        PaymentWebhookResult result,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        ProcessedWebhookEvent Record(
            WebhookOutcome outcome, PaymentIntentId? intentId = null, string? detail = null) =>
            ProcessedWebhookEvent.Record(
                WebhookEventId.From(_guids.NewSequentialGuid()),
                provider,
                eventId,
                outcome,
                now,
                result.EventKind,
                result.ExternalReference,
                intentId,
                detail);

        if (!result.IsRecognized || string.IsNullOrWhiteSpace(result.ExternalReference))
        {
            return (Record(WebhookOutcome.Ignored, detail: "Not a payment this platform tracks."), false);
        }

        var intent = await FindIntentAsync(provider, result.ExternalReference, cancellationToken);

        if (intent is null)
        {
            return (Record(WebhookOutcome.Unmatched, detail: "No payment matched that reference."), false);
        }

        // A callback naming a different currency from the intent it claims to be about is either a
        // provider defect or somebody replaying one intent's signature against another. Either way
        // the platform does not convert, so there is no reading of it that is safe to act on.
        if (result.Currency is { } currency
            && Money.NormalizeCurrency(currency) is { } normalized
            && !string.Equals(normalized, intent.Currency, StringComparison.Ordinal))
        {
            return (
                Record(WebhookOutcome.Refused, intent.Id, "The callback's currency is not the payment's."),
                false);
        }

        try
        {
            var changed = await TransitionAsync(provider, intent, result, cancellationToken);

            return changed.IsSuccess
                ? (Record(WebhookOutcome.Applied, intent.Id, result.EventKind), true)
                : (Record(WebhookOutcome.Refused, intent.Id, changed.Error.Code), false);
        }
        catch (DomainException exception)
        {
            // The state machine said no. That is the aggregate doing its job — a redelivered capture
            // for an intent that is already captured lands here — and it is recorded rather than
            // thrown, so the provider gets a 200 and stops redelivering.
            _logger.LogInformation(
                "A {Provider} '{EventKind}' callback was refused by the payment state machine: {Code}.",
                provider,
                result.EventKind,
                exception.Code);

            return (Record(WebhookOutcome.Refused, intent.Id, exception.Code), false);
        }
    }

    /// <summary>
    /// Finds the intent a callback is about.
    /// <para>
    /// By the provider's own reference first, which is the join key. A provider that echoes the
    /// platform's identifier back — several do, and the shipped simulator does when it has nothing of
    /// its own — is handled second, so a callback that arrives before the authorisation response has
    /// been persisted still lands on the right intent instead of on none.
    /// </para>
    /// </summary>
    private async Task<PaymentIntent?> FindIntentAsync(
        string provider, string externalReference, CancellationToken cancellationToken)
    {
        var byReference = await _intents.FindByExternalReferenceAsync(
            provider, externalReference, cancellationToken);

        if (byReference is not null)
        {
            return byReference;
        }

        return Guid.TryParse(externalReference, out var parsed)
            ? await _intents.FindAsync(PaymentIntentId.From(parsed), cancellationToken)
            : null;
    }

    private async Task<Result> TransitionAsync(
        string provider,
        PaymentIntent intent,
        PaymentWebhookResult result,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        // The provider that signed the callback, not one from its body. An intent that has never
        // seen an adapter takes the name from the verified route segment.
        var gateway = GatewayReference.Create(
            intent.GatewayProvider ?? provider,
            result.ExternalReference!,
            result.ExternalStatus);

        switch (result.EventKind?.Trim().ToLowerInvariant())
        {
            case "authorized":
                intent.Authorize(_guids.NewSequentialGuid(), gateway, now);
                _intents.Update(intent);

                return Result.Success();

            case "captured":
            {
                var amount = await AmountAsync(intent, result, cancellationToken);
                var captured = await _payments.ConfirmCaptureAsync(
                    intent, amount, gateway, cancellationToken);

                return captured.IsSuccess ? Result.Success() : Result.Failure(captured.Error);
            }

            case "failed":
                intent.Fail(
                    _guids.NewSequentialGuid(),
                    result.FailureCode,
                    result.FailureMessage,
                    isRetryable: false,
                    now,
                    intent.GatewayProvider);
                _intents.Update(intent);

                return Result.Success();

            case "cancelled":
                intent.Cancel(now);
                _intents.Update(intent);

                return Result.Success();

            case "settled":
                intent.Settle(now, result.ExternalStatus);
                _intents.Update(intent);

                return Result.Success();

            case "refunded":
            {
                var amount = await AmountAsync(intent, result, cancellationToken);
                var refunded = await _payments.ConfirmRefundAsync(
                    intent,
                    amount,
                    RefundReason.RequestedByPayer,
                    result.ExternalReference,
                    cancellationToken);

                return refunded.IsSuccess ? Result.Success() : Result.Failure(refunded.Error);
            }

            default:
                return Result.Failure(Error.Validation(
                    FinancialErrorCodes.WebhookUnrecognized,
                    "That event kind is not one this platform acts on."));
        }
    }

    /// <summary>
    /// The amount a callback is about, in minor units, defaulting to the whole intent.
    /// <para>
    /// The provider's figure is taken as-is and then handed to the aggregate, which refuses anything
    /// larger than what was authorised or, for a refund, larger than what remains. The check belongs
    /// there rather than here for the reason the class summary gives: one place decides what is
    /// legal.
    /// </para>
    /// </summary>
    private async Task<Money> AmountAsync(
        PaymentIntent intent, PaymentWebhookResult result, CancellationToken cancellationToken)
    {
        var facts = await _currencies.FactsAsync(intent.Currency, cancellationToken);

        return result.AmountMinorUnits is { } minorUnits and >= 0
            ? Money.FromMinorUnits(minorUnits, facts.Code, facts.Exponent)
            : intent.Amount;
    }
}
