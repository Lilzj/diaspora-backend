using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Webhooks;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Financial.Application.Webhooks;

/// <summary>
/// Turns a payout provider's callback into a state change, or refuses it.
/// <para>
/// <b>The same four gates the payment path uses, in the same order, and none of them skippable.</b>
/// A registered processor for the named provider; a signature that verifies over the raw body; an
/// event identifier not seen before; and a transition the withdrawal aggregate allows. A callback
/// failing any of them is recorded and does nothing.
/// </para>
/// <para>
/// <b>A callback and the resolution sweep are two routes to the same answer, and neither is
/// allowed to be the only one.</b> A webhook arrives in seconds and a deployment whose endpoint is
/// unreachable for a day would otherwise hold a member's money indefinitely; the sweep asks and is
/// slower but cannot be blocked from outside. They converge because both end in the same aggregate
/// transitions, and a second arrival — whichever came first — is refused by the state machine
/// rather than applied twice.
/// </para>
/// <para>
/// <b>A withdrawal is matched on the platform's own reference, never the provider's.</b> Every
/// adapter sends the withdrawal identifier out with the instruction precisely so a callback about a
/// submission whose response was lost can still be matched.
/// </para>
/// </summary>
public sealed class ProcessPayoutWebhookCommandHandler
    : ICommandHandler<ProcessPayoutWebhookCommand, WebhookAcknowledgementResponse>
{
    private readonly IEnumerable<IPayoutWebhookProcessor> _processors;
    private readonly IProcessedWebhookEventRepository _events;
    private readonly IWithdrawalRepository _withdrawals;
    private readonly WithdrawalProcessor _payouts;
    private readonly FinancialIntegrationEventRelay _relay;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly ILogger<ProcessPayoutWebhookCommandHandler> _logger;

    public ProcessPayoutWebhookCommandHandler(
        IEnumerable<IPayoutWebhookProcessor> processors,
        IProcessedWebhookEventRepository events,
        IWithdrawalRepository withdrawals,
        WithdrawalProcessor payouts,
        FinancialIntegrationEventRelay relay,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock,
        ILogger<ProcessPayoutWebhookCommandHandler> logger)
    {
        _processors = processors;
        _events = events;
        _withdrawals = withdrawals;
        _payouts = payouts;
        _relay = relay;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<WebhookAcknowledgementResponse>> Handle(
        ProcessPayoutWebhookCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var provider = ProcessedWebhookEvent.NormalizeProvider(command.Provider);

        var processor = _processors.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase));

        if (processor is null)
        {
            // Not recorded: nothing to de-duplicate against and no signature to have verified, so a
            // row here would be an unauthenticated caller writing to the database.
            FinancialTelemetry.WebhooksReceived.Add(
                1, new KeyValuePair<string, object?>("outcome", "payout_provider_unknown"));

            return Result.Failure<WebhookAcknowledgementResponse>(Error.NotFound(
                FinancialErrorCodes.WebhookProviderUnknown,
                "No payout callback processor is registered for that provider."));
        }

        var result = await processor.ProcessAsync(
            new PayoutWebhookEnvelope
            {
                Provider = provider,
                RawBody = command.RawBody,
                Headers = command.Headers,
                ReceivedAt = _clock.UtcNow
            },
            cancellationToken);

        if (!result.IsVerified)
        {
            FinancialTelemetry.WebhooksReceived.Add(
                1,
                new KeyValuePair<string, object?>("provider", provider),
                new KeyValuePair<string, object?>("outcome", "unverified"));

            _logger.LogWarning(
                "Rejected a {Provider} payout callback whose signature did not verify.", provider);

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

        var (record, withdrawal) = await ApplyAsync(provider, eventId, result, cancellationToken);

        _events.Add(record);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (withdrawal is not null)
        {
            await _relay.PublishAsync(withdrawal, cancellationToken);
        }

        FinancialTelemetry.WebhooksReceived.Add(
            1,
            new KeyValuePair<string, object?>("provider", provider),
            new KeyValuePair<string, object?>("outcome", record.Outcome.ToString()));

        return Result.Success(new WebhookAcknowledgementResponse
        {
            Outcome = record.Outcome.ToString(),
            WasDuplicate = false,
            Detail = record.Detail
        });
    }

    private async Task<(ProcessedWebhookEvent Record, Withdrawal? Changed)> ApplyAsync(
        string provider,
        string eventId,
        PayoutWebhookResult result,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        ProcessedWebhookEvent Record(WebhookOutcome outcome, string? detail = null) =>
            ProcessedWebhookEvent.Record(
                WebhookEventId.From(_guids.NewSequentialGuid()),
                provider,
                eventId,
                outcome,
                now,
                result.EventKind,
                result.WithdrawalReference,
                paymentIntentId: null,
                detail);

        if (!result.IsRecognized || string.IsNullOrWhiteSpace(result.WithdrawalReference))
        {
            return (Record(WebhookOutcome.Ignored, "Not a payout this platform tracks."), null);
        }

        var withdrawal = await FindAsync(result.WithdrawalReference, cancellationToken);

        if (withdrawal is null)
        {
            // A 200 with nothing done. Answering 4xx for a payout this deployment has never heard of
            // buys an unbounded redelivery loop against a reference that will never resolve.
            return (Record(WebhookOutcome.Unmatched, "No withdrawal matched that reference."), null);
        }

        // The provider that took the instruction is the only one entitled to report on it. A
        // callback from another naming this withdrawal is either a misconfiguration or somebody
        // replaying one provider's signature against another's payout.
        if (!string.IsNullOrWhiteSpace(withdrawal.SettlementProvider)
            && !string.Equals(withdrawal.SettlementProvider, provider, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "A {Provider} callback named withdrawal {WithdrawalId}, which was submitted to "
                + "{Owner}. Refused.",
                provider,
                withdrawal.Id.Value,
                withdrawal.SettlementProvider);

            return (Record(WebhookOutcome.Refused, "That payout belongs to another provider."), null);
        }

        try
        {
            return await TransitionAsync(withdrawal, result, now, cancellationToken)
                ? (Record(WebhookOutcome.Applied, result.EventKind), withdrawal)
                : (Record(WebhookOutcome.Ignored, "Nothing to apply."), null);
        }
        catch (DomainException exception)
        {
            // The state machine said no. A redelivered completion for an already-completed
            // withdrawal lands here, and it is recorded rather than thrown so the provider gets a
            // 200 and stops redelivering.
            _logger.LogInformation(
                "A {Provider} '{EventKind}' payout callback was refused by the withdrawal state "
                + "machine: {Code}.",
                provider,
                result.EventKind,
                exception.Code);

            return (Record(WebhookOutcome.Refused, exception.Code), null);
        }
    }

    /// <summary>
    /// Applies one event to the withdrawal. True when something changed.
    /// <para>
    /// Three kinds and no more: a payout left, did not, or came back. Every provider-specific stage
    /// in between is one of those three to the member whose money it is.
    /// </para>
    /// </summary>
    private async Task<bool> TransitionAsync(
        Withdrawal withdrawal,
        PayoutWebhookResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        switch (result.EventKind?.ToLowerInvariant())
        {
            case "paid":
                withdrawal.MarkCompleted(now, result.ProviderReference);

                var paid = await _payouts.PayOutAsync(withdrawal, cancellationToken);

                if (paid.IsFailure)
                {
                    _logger.LogError(
                        "Withdrawal {WithdrawalId} was reported paid and could not be posted: "
                        + "{Code}. The ledger and the provider now disagree.",
                        withdrawal.Id.Value,
                        paid.Error.Code);
                }

                break;

            case "failed":
                withdrawal.MarkFailed(
                    result.FailureCode ?? "settlement.failed",
                    result.FailureMessage ?? "The payout provider reported it failed.",
                    now);

                var released = await _payouts.ReleaseAsync(
                    withdrawal,
                    result.FailureMessage ?? "The payout provider reported it failed.",
                    cancellationToken);

                if (released.IsFailure)
                {
                    _logger.LogError(
                        "Withdrawal {WithdrawalId} failed and its hold could not be released: {Code}.",
                        withdrawal.Id.Value,
                        released.Error.Code);
                }

                break;

            case "reversed":
                // A new fact, never an undo. The money went out and came back, and the ledger says
                // so in a compensating entry rather than by rewinding the payout.
                withdrawal.MarkReversed(result.FailureCode, result.FailureMessage, now);

                var reversed = await _payouts.ReverseAsync(
                    withdrawal,
                    result.FailureMessage ?? "The payout was returned by the provider.",
                    cancellationToken);

                if (reversed.IsFailure)
                {
                    _logger.LogError(
                        "Withdrawal {WithdrawalId} was reversed and could not be posted: {Code}.",
                        withdrawal.Id.Value,
                        reversed.Error.Code);
                }

                break;

            default:
                return false;
        }

        _withdrawals.Update(withdrawal);

        return true;
    }

    /// <summary>
    /// Finds the withdrawal a callback names.
    /// <para>
    /// The reference an adapter sends out is the withdrawal's identifier formatted without dashes,
    /// so a callback carrying it back parses straight to an id. Anything that does not parse is a
    /// reference from somewhere else, and is unmatched rather than searched for.
    /// </para>
    /// </summary>
    private async Task<Withdrawal?> FindAsync(string reference, CancellationToken cancellationToken) =>
        Guid.TryParse(reference.Trim(), out var id)
            ? await _withdrawals.FindAsync(WithdrawalId.From(id), cancellationToken)
            : null;
}
