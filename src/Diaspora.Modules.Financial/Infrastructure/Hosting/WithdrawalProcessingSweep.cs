using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Modules.Financial.Infrastructure.Payouts;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Hands held withdrawals to a settlement provider and records what it says.
/// <para>
/// <b>Does nothing at all when no provider is registered, and that is the honest behaviour rather
/// than a limitation.</b> No settlement engine ships in this assembly — an architecture test asserts
/// the absence — so a deployment that has not registered one has requests sitting in
/// <c>Requested</c> with the money genuinely held. That is a true state of the world: the member
/// asked, the funds are ring-fenced, and nobody has paid them yet. The alternative, marking them
/// complete, would be the platform telling somebody their money had been sent when nothing had left.
/// </para>
/// <para>
/// <b>The two-phase shape is what makes a crash survivable.</b> The request is marked
/// <c>Processing</c> and saved <em>before</em> the provider is called, so an instruction that was
/// submitted and whose response was lost is found in <c>Processing</c> on the next pass rather than
/// being submitted a second time. A sweep that called first and recorded afterwards would pay twice
/// for every timeout.
/// </para>
/// <para>
/// <b>Every withdrawal is routed on its own, and nothing here takes the first registered
/// provider.</b> It used to: <c>GetServices&lt;ISettlementProvider&gt;().FirstOrDefault()</c> handed
/// a Kenyan mobile-money payout to whichever adapter happened to register first. Provider
/// availability is a property of the corridor rather than of the deployment, so the route is
/// resolved per request, from the destination's own country and rail.
/// </para>
/// <para>
/// <b>An unknown outcome holds the money; only a refusal releases it.</b> A provider that timed out
/// may already have sent the payment, so treating that as a refusal and giving the balance back pays
/// the member twice. The withdrawal stays in <c>Processing</c> — which is exactly what that status
/// means — until something establishes what happened. That is worse for a member whose payout
/// genuinely failed, because their money is held longer; it is the right trade, because a held
/// balance is recoverable by an operator and a double payment is not.
/// </para>
/// <para>
/// <b>Unknown outcomes are resolved on a later pass, when a deployment registers somewhere to
/// ask.</b> <c>ISettlementStatusProvider</c> is optional: with one, a stuck withdrawal is completed
/// or failed automatically once the provider will say which; without one it stays held and visible
/// rather than being guessed at. The resolution pass runs before the submission pass so that money
/// already committed is accounted for before more is.
/// </para>
/// <para>
/// <b>The route is resolved again here, not trusted from request time.</b> Hours can pass between a
/// member asking and a sweep running, and in that window a route can be switched off, an adapter can
/// be removed, or Trust can revoke the member's verification. A withdrawal that can no longer be
/// carried is failed and its hold released - the money goes back and they can ask again - which is
/// the same safe direction a provider refusal takes.
/// </para>
/// </summary>
public sealed class WithdrawalProcessingSweep : FinancialSweep
{
    public WithdrawalProcessingSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<WithdrawalProcessingSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableWithdrawalSweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.WithdrawalSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var providers = scope.ServiceProvider.GetServices<ISettlementProvider>().ToList();

        if (providers.Count == 0)
        {
            return 0;
        }

        var resolver = scope.ServiceProvider.GetRequiredService<PayoutRouteResolver>();
        var destinations = scope.ServiceProvider.GetRequiredService<IPayoutDestinationRepository>();
        var accounts = scope.ServiceProvider.GetRequiredService<IFinancialAccountRepository>();
        var withdrawals = scope.ServiceProvider.GetRequiredService<IWithdrawalRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<WithdrawalProcessor>();
        var relay = scope.ServiceProvider.GetRequiredService<FinancialIntegrationEventRelay>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var handled = await ResolveUnknownOutcomesAsync(scope, cancellationToken);

        var pending = await withdrawals.ListPendingAsync(Options.SweepBatchSize, cancellationToken);

        foreach (var withdrawal in pending)
        {
            var now = clock.UtcNow;

            var routed = await ResolveAsync(
                withdrawal, resolver, destinations, accounts, providers, cancellationToken);

            if (routed is null)
            {
                // Nothing can carry it any more. Fail it and give the money back rather than leaving
                // a hold nobody will ever release; the member can ask again once a route exists.
                const string Reason = "No payout provider can carry this any more.";

                withdrawal.MarkFailed(
                    FinancialErrorCodes.PayoutNotSupported, Reason, clock.UtcNow);

                var releasedUnroutable = await processor.ReleaseAsync(
                    withdrawal, Reason, cancellationToken);

                if (releasedUnroutable.IsFailure)
                {
                    Logger.LogError(
                        "Withdrawal {WithdrawalId} became unroutable and its hold could not be "
                        + "released: {Code}.",
                        withdrawal.Id.Value,
                        releasedUnroutable.Error.Code);
                }

                withdrawals.Update(withdrawal);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                await relay.PublishAsync(withdrawal, cancellationToken);

                FinancialTelemetry.WalletMovements.Add(
                    1,
                    new KeyValuePair<string, object?>("operation", "withdrawal"),
                    new KeyValuePair<string, object?>("outcome", withdrawal.Status.ToString()));

                handled++;
                continue;
            }

            var (settlement, destination) = routed.Value;

            // Recorded before the call. See the class summary: a submitted instruction whose response
            // was lost must not be submitted again.
            withdrawal.MarkProcessing(settlement.Provider, settlementReference: null, now);
            withdrawals.Update(withdrawal);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var outcome = await SubmitAsync(settlement, withdrawal, destination, cancellationToken);

            await ApplyAsync(withdrawal, outcome, processor, clock, cancellationToken);

            withdrawals.Update(withdrawal);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await relay.PublishAsync(withdrawal, cancellationToken);

            FinancialTelemetry.WalletMovements.Add(
                1,
                new KeyValuePair<string, object?>("operation", "withdrawal"),
                new KeyValuePair<string, object?>("outcome", withdrawal.Status.ToString()));

            handled++;
        }

        return handled;
    }

    /// <summary>
    /// The adapter that may carry this withdrawal now, or null when none may.
    /// <para>
    /// Re-resolved rather than remembered. The inputs are all read fresh: the destination's country
    /// and rail, the account's verification status, and the net amount the provider will actually
    /// move. A withdrawal whose destination has since been removed, or whose owner Trust has since
    /// restricted, is not carried.
    /// </para>
    /// </summary>
    private async Task<(ISettlementProvider Provider, PayoutDestination Destination)?> ResolveAsync(
        Withdrawal withdrawal,
        PayoutRouteResolver resolver,
        IPayoutDestinationRepository destinations,
        IFinancialAccountRepository accounts,
        IReadOnlyList<ISettlementProvider> providers,
        CancellationToken cancellationToken)
    {
        var destination = await destinations.FindAsync(withdrawal.DestinationId, cancellationToken);

        if (destination is null || destination.IsRemoved)
        {
            Logger.LogWarning(
                "Withdrawal {WithdrawalId} has no usable payout destination and cannot be routed.",
                withdrawal.Id.Value);

            return null;
        }

        var account = await accounts.FindAsync(withdrawal.AccountId, cancellationToken);

        var route = await resolver.ResolveAsync(
            new PayoutRouteRequest
            {
                Country = destination.CountryCode ?? string.Empty,
                Method = destination.Kind,

                // What the provider is asked to move, which is what its limits apply to.
                Amount = withdrawal.Net,
                PayeeVerified = account?.CanDebit ?? false,

                // The destination's token was issued by one provider and is unreadable to any
                // other, so a bound destination pins the route.
                RequiredProvider = destination.Provider
            },
            withdrawal.RequestedByUserId,
            cancellationToken);

        if (!route.IsRouted)
        {
            Logger.LogWarning(
                "Withdrawal {WithdrawalId} can no longer be routed: {Reason}. {Detail}",
                withdrawal.Id.Value,
                route.Refusal,
                route.Detail);

            return null;
        }

        var settlement = providers.FirstOrDefault(provider =>
            string.Equals(provider.Provider, route.Provider, StringComparison.OrdinalIgnoreCase));

        if (settlement is null)
        {
            Logger.LogError(
                "The capability matrix routed withdrawal {WithdrawalId} to {Provider}, which no "
                + "registered adapter answers to.",
                withdrawal.Id.Value,
                route.Provider);

            return null;
        }

        return (settlement, destination);
    }

    /// <summary>
    /// Asks the provider to send the money.
    /// <para>
    /// <b>A provider that throws has said "unknown", not "no".</b> This is the reverse of what this
    /// method used to do: it caught everything and returned false, so a timeout released the hold
    /// and a provider that had in fact sent the money left the member paid twice. Catching still
    /// keeps one bad adapter from taking down a pass that is processing other people's money — but
    /// the answer it produces now holds the funds instead of giving them away.
    /// </para>
    /// </summary>
    private async Task<SettlementOutcome> SubmitAsync(
        ISettlementProvider settlement,
        Withdrawal withdrawal,
        PayoutDestination destination,
        CancellationToken cancellationToken)
    {
        try
        {
            return await settlement.SubmitAsync(
                new SettlementInstruction
                {
                    CounterpartyId = withdrawal.RequestedByUserId,
                    Amount = withdrawal.Net,
                    PeriodStart = withdrawal.RequestedAt,
                    PeriodEnd = withdrawal.ProcessingStartedAt ?? withdrawal.RequestedAt,

                    // The withdrawal's own identifier, so a provider can be asked about it later and
                    // a redelivered instruction is recognisable as the same one.
                    Reference = withdrawal.Id.Value.ToString("N"),

                    // Where it is going, in the provider's own terms. Without these an adapter knows
                    // a Diaspora user identifier and nothing it can act on.
                    DestinationReference = destination.ProviderToken,
                    DestinationProvider = destination.Provider,
                    DestinationKind = destination.Kind,
                    DestinationCountry = destination.CountryCode,
                    DestinationLabel = destination.Label
                },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Logger.LogError(
                exception,
                "The settlement provider threw on withdrawal {WithdrawalId}. The outcome is unknown, "
                + "so the hold stays and the request waits to be resolved.",
                withdrawal.Id.Value);

            return SettlementOutcome.Unresolved(
                "settlement.threw", "The settlement provider did not answer.");
        }
    }

    /// <summary>
    /// Turns one outcome into what happens to the money.
    /// <para>
    /// The three branches are the point of the type: accepted pays, refused releases, and unknown
    /// does neither. Only a positive refusal is allowed to give a balance back.
    /// </para>
    /// </summary>
    private async Task ApplyAsync(
        Withdrawal withdrawal,
        SettlementOutcome outcome,
        WithdrawalProcessor processor,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (outcome.IsAccepted)
        {
            withdrawal.MarkCompleted(clock.UtcNow, outcome.ProviderReference);

            var paid = await processor.PayOutAsync(withdrawal, cancellationToken);

            if (paid.IsFailure)
            {
                Logger.LogError(
                    "Withdrawal {WithdrawalId} was paid by {Provider} and could not be posted: "
                    + "{Code}. The ledger and the provider now disagree.",
                    withdrawal.Id.Value,
                    withdrawal.SettlementProvider,
                    paid.Error.Code);
            }

            return;
        }

        if (!outcome.MayReleaseHold)
        {
            // Held, on purpose. See the class summary: giving this balance back could pay twice.
            withdrawal.RecordUnresolvedOutcome(
                outcome.Code ?? "settlement.unknown", outcome.Message, clock.UtcNow);

            Logger.LogWarning(
                "Withdrawal {WithdrawalId} has an unresolved outcome from {Provider} ({Code}). "
                + "The hold stays until something establishes what happened.",
                withdrawal.Id.Value,
                withdrawal.SettlementProvider,
                outcome.Code);

            return;
        }

        var reason = outcome.Message ?? "The settlement provider refused it.";

        withdrawal.MarkFailed(outcome.Code ?? "settlement.refused", reason, clock.UtcNow);

        var released = await processor.ReleaseAsync(withdrawal, reason, cancellationToken);

        if (released.IsFailure)
        {
            Logger.LogError(
                "Withdrawal {WithdrawalId} failed and its hold could not be released: {Code}.",
                withdrawal.Id.Value,
                released.Error.Code);
        }
    }

    /// <summary>
    /// Asks a status provider what became of the withdrawals whose outcome nobody knows.
    /// <para>
    /// Does nothing without a registered <c>ISettlementStatusProvider</c>, which is honest rather
    /// than limiting: guessing at an unknown outcome is the failure this whole path exists to avoid.
    /// Instructions submitted within the grace window are left alone — a provider asked about a
    /// transfer it is still creating answers "not found", and that is not an answer.
    /// </para>
    /// </summary>
    private async Task<int> ResolveUnknownOutcomesAsync(
        IServiceScope scope, CancellationToken cancellationToken)
    {
        var statusProviders = scope.ServiceProvider.GetServices<ISettlementStatusProvider>().ToList();

        if (statusProviders.Count == 0)
        {
            return 0;
        }

        var withdrawals = scope.ServiceProvider.GetRequiredService<IWithdrawalRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<WithdrawalProcessor>();
        var relay = scope.ServiceProvider.GetRequiredService<FinancialIntegrationEventRelay>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var grace = TimeSpan.FromMinutes(Math.Max(1, Options.WithdrawalResolutionGraceMinutes));

        var unresolved = await withdrawals.ListUnresolvedAsync(
            clock.UtcNow - grace, Options.SweepBatchSize, cancellationToken);

        var resolved = 0;

        foreach (var withdrawal in unresolved)
        {
            var status = statusProviders.FirstOrDefault(candidate => string.Equals(
                candidate.Provider, withdrawal.SettlementProvider, StringComparison.OrdinalIgnoreCase));

            // Nothing registered can answer for the provider that took this one. It stays held.
            if (status is null)
            {
                continue;
            }

            var outcome = await AskAsync(status, withdrawal, cancellationToken);

            if (outcome.Disposition == SettlementDisposition.Unknown)
            {
                withdrawal.RecordUnresolvedOutcome(
                    outcome.Code ?? "settlement.unknown", outcome.Message, clock.UtcNow);

                withdrawals.Update(withdrawal);
                await unitOfWork.SaveChangesAsync(cancellationToken);

                continue;
            }

            await ApplyAsync(withdrawal, outcome, processor, clock, cancellationToken);

            withdrawals.Update(withdrawal);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await relay.PublishAsync(withdrawal, cancellationToken);

            FinancialTelemetry.WalletMovements.Add(
                1,
                new KeyValuePair<string, object?>("operation", "withdrawal.resolved"),
                new KeyValuePair<string, object?>("outcome", withdrawal.Status.ToString()));

            resolved++;
        }

        return resolved;
    }

    private async Task<SettlementOutcome> AskAsync(
        ISettlementStatusProvider status, Withdrawal withdrawal, CancellationToken cancellationToken)
    {
        try
        {
            return await status.GetOutcomeAsync(
                withdrawal.Id.Value.ToString("N"), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            Logger.LogWarning(
                exception,
                "Could not establish the outcome of withdrawal {WithdrawalId}. It stays held.",
                withdrawal.Id.Value);

            return SettlementOutcome.Unresolved(
                "settlement.status_unavailable", "The provider could not be asked.");
        }
    }
}
