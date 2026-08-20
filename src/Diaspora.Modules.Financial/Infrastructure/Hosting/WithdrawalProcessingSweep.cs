using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Settlement;
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

        var settlement = scope.ServiceProvider.GetServices<ISettlementProvider>().FirstOrDefault();

        if (settlement is null)
        {
            return 0;
        }

        var withdrawals = scope.ServiceProvider.GetRequiredService<IWithdrawalRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<WithdrawalProcessor>();
        var relay = scope.ServiceProvider.GetRequiredService<FinancialIntegrationEventRelay>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var pending = await withdrawals.ListPendingAsync(Options.SweepBatchSize, cancellationToken);
        var handled = 0;

        foreach (var withdrawal in pending)
        {
            var now = clock.UtcNow;

            // Recorded before the call. See the class summary: a submitted instruction whose response
            // was lost must not be submitted again.
            withdrawal.MarkProcessing(settlement.Provider, settlementReference: null, now);
            withdrawals.Update(withdrawal);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var accepted = await SubmitAsync(settlement, withdrawal, cancellationToken);

            if (accepted)
            {
                withdrawal.MarkCompleted(clock.UtcNow);

                var paid = await processor.PayOutAsync(withdrawal, cancellationToken);

                if (paid.IsFailure)
                {
                    Logger.LogError(
                        "Withdrawal {WithdrawalId} was paid by {Provider} and could not be posted: "
                        + "{Code}. The ledger and the provider now disagree.",
                        withdrawal.Id.Value,
                        settlement.Provider,
                        paid.Error.Code);
                }
            }
            else
            {
                withdrawal.MarkFailed(
                    "settlement.refused", "The settlement provider refused it.", clock.UtcNow);

                var released = await processor.ReleaseAsync(
                    withdrawal, "The settlement provider refused it.", cancellationToken);

                if (released.IsFailure)
                {
                    Logger.LogError(
                        "Withdrawal {WithdrawalId} failed and its hold could not be released: {Code}.",
                        withdrawal.Id.Value,
                        released.Error.Code);
                }
            }

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
    /// Asks the provider to send the money.
    /// <para>
    /// A provider that throws is treated as a refusal rather than left to take the pass down, and the
    /// hold is released — the member's money goes back to them and they can try again, which is the
    /// safe direction when the outcome is unknown. A provider that actually did send would surface as
    /// a reconciliation mismatch, which is a problem with a person attached rather than a member
    /// whose funds are stuck.
    /// </para>
    /// </summary>
    private async Task<bool> SubmitAsync(
        ISettlementProvider settlement, Withdrawal withdrawal, CancellationToken cancellationToken)
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
                    Reference = withdrawal.Id.Value.ToString("N")
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
                "The settlement provider threw on withdrawal {WithdrawalId}. Treating it as refused "
                + "and releasing the hold.",
                withdrawal.Id.Value);

            return false;
        }
    }
}
