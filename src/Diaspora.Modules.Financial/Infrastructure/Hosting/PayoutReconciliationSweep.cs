using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Payouts;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Checks what the platform believes about its payouts against what the provider says it paid.
/// <para>
/// <b>This is the only check that looks at the provider's side first, and it is the only one that
/// can find money that left without a withdrawal behind it.</b> The status provider resolves
/// withdrawals the platform already has a question about; a webhook tells the platform about a
/// payout it started. Neither can see a transfer the platform never recorded — and that is the most
/// serious thing a payout system can get wrong.
/// </para>
/// <para>
/// <b>Where the two disagree, the rules are written down here rather than emerging from whichever
/// branch somebody wrote first.</b>
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Provider paid, platform is still Processing.</b> Completed, and the hold becomes a payout.
/// The ordinary case and the only one that is not a problem: the platform submitted, the provider
/// confirms, and the gap is the delay this sweep exists to close.
/// </item>
/// <item>
/// <b>Provider failed, platform is still Processing.</b> Failed, and the hold is released. Safe
/// because it is a positive statement that no money moved — the same statement that justifies a
/// release anywhere else on this path.
/// </item>
/// <item>
/// <b>Provider paid, and no withdrawal matches.</b> <em>Nothing is applied.</em> An orphan is money
/// that left the platform's provider account with nothing here accounting for it, and the platform
/// does not manufacture a withdrawal to explain it — inventing one would put a debit on a member's
/// wallet for a payout they never asked for. Logged at error and counted, for a person.
/// </item>
/// <item>
/// <b>Platform says Completed, provider has no record.</b> Also nothing applied, and the other
/// serious direction: the platform believes money left that the provider never sent, so a member
/// has been debited for nothing.
/// </item>
/// <item>
/// <b>Amounts disagree.</b> Nothing applied. A withdrawal whose provider record is for a different
/// figure is not a withdrawal this sweep can reason about, and picking either number would be the
/// platform choosing which of two contradictory facts about somebody's money to believe.
/// </item>
/// </list>
/// <para>
/// <b>Does nothing when no reconciliation provider is registered.</b> None ships. The counter is
/// exported regardless so a deployment can see that reconciliation is not running rather than
/// assuming it is.
/// </para>
/// </summary>
public sealed class PayoutReconciliationSweep : FinancialSweep
{
    public PayoutReconciliationSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<PayoutReconciliationSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnablePayoutReconciliationSweep;

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Options.PayoutReconciliationSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var providers = scope.ServiceProvider
            .GetServices<IPayoutReconciliationProvider>().ToList();

        if (providers.Count == 0)
        {
            return 0;
        }

        var withdrawals = scope.ServiceProvider.GetRequiredService<IWithdrawalRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<WithdrawalProcessor>();
        var relay = scope.ServiceProvider.GetRequiredService<FinancialIntegrationEventRelay>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var to = clock.UtcNow;
        var from = to.AddHours(-Math.Max(1, Options.PayoutReconciliationWindowHours));
        var examined = 0;

        foreach (var provider in providers)
        {
            IReadOnlyList<PayoutReconciliationRecord> records;

            try
            {
                records = await provider.FetchAsync(from, to, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031
            catch (Exception exception)
#pragma warning restore CA1031
            {
                // One provider being unreachable must not stop the others being checked.
                Logger.LogError(
                    exception,
                    "Could not fetch {Provider}'s payout records. Reconciliation did not run for it.",
                    provider.Provider);

                continue;
            }

            foreach (var record in records)
            {
                examined++;

                var withdrawal = await MatchAsync(record, withdrawals, cancellationToken);

                if (withdrawal is null)
                {
                    // The finding this sweep exists for.
                    Logger.LogError(
                        "{Provider} reports a {Status} payout of {Amount} {Currency} "
                        + "({ProviderReference}) that no withdrawal accounts for. Nothing has been "
                        + "applied; this needs a person.",
                        record.Provider,
                        record.Status,
                        record.AmountMinorUnits,
                        record.Currency,
                        record.ProviderReference);

                    FinancialTelemetry.ReconciliationMismatches.Add(
                        1,
                        new KeyValuePair<string, object?>("provider", record.Provider),
                        new KeyValuePair<string, object?>("kind", "orphan_payout"));

                    continue;
                }

                if (!Agrees(record, withdrawal))
                {
                    Logger.LogError(
                        "{Provider} reports withdrawal {WithdrawalId} as {Amount} {Currency}; the "
                        + "platform holds {Held} {HeldCurrency}. Nothing has been applied.",
                        record.Provider,
                        withdrawal.Id.Value,
                        record.AmountMinorUnits,
                        record.Currency,
                        withdrawal.Net.MinorUnits,
                        withdrawal.Currency);

                    FinancialTelemetry.ReconciliationMismatches.Add(
                        1,
                        new KeyValuePair<string, object?>("provider", record.Provider),
                        new KeyValuePair<string, object?>("kind", "amount_mismatch"));

                    continue;
                }

                if (await ApplyAsync(record, withdrawal, processor, clock, cancellationToken))
                {
                    withdrawals.Update(withdrawal);
                    await unitOfWork.SaveChangesAsync(cancellationToken);
                    await relay.PublishAsync(withdrawal, cancellationToken);
                }
            }
        }

        return examined;
    }

    /// <summary>
    /// Applies the two transitions reconciliation is allowed to make, and refuses the rest.
    /// <para>
    /// Only a withdrawal the platform believes is still in flight may be moved from here. One the
    /// platform already considers finished is either in agreement — nothing to do — or in
    /// disagreement, and a batch job is the wrong place to resolve that.
    /// </para>
    /// </summary>
    private async Task<bool> ApplyAsync(
        PayoutReconciliationRecord record,
        Withdrawal withdrawal,
        WithdrawalProcessor processor,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (withdrawal.Status != WithdrawalStatus.Processing)
        {
            if (IsDisagreement(record, withdrawal))
            {
                Logger.LogError(
                    "{Provider} reports withdrawal {WithdrawalId} as {Status}; the platform has it "
                    + "as {Held}. Nothing has been applied.",
                    record.Provider,
                    withdrawal.Id.Value,
                    record.Status,
                    withdrawal.Status);

                FinancialTelemetry.ReconciliationMismatches.Add(
                    1,
                    new KeyValuePair<string, object?>("provider", record.Provider),
                    new KeyValuePair<string, object?>("kind", "status_mismatch"));
            }

            return false;
        }

        switch (record.Status.ToLowerInvariant())
        {
            case "paid":
                withdrawal.MarkCompleted(clock.UtcNow, record.ProviderReference);

                var paid = await processor.PayOutAsync(withdrawal, cancellationToken);

                if (paid.IsFailure)
                {
                    Logger.LogError(
                        "Withdrawal {WithdrawalId} reconciled as paid and could not be posted: "
                        + "{Code}.",
                        withdrawal.Id.Value,
                        paid.Error.Code);
                }

                return true;

            case "failed":
                const string Reason = "The provider's own record says it failed.";

                withdrawal.MarkFailed("settlement.reconciled_failed", Reason, clock.UtcNow);

                var released = await processor.ReleaseAsync(withdrawal, Reason, cancellationToken);

                if (released.IsFailure)
                {
                    Logger.LogError(
                        "Withdrawal {WithdrawalId} reconciled as failed and its hold could not be "
                        + "released: {Code}.",
                        withdrawal.Id.Value,
                        released.Error.Code);
                }

                return true;

            default:
                // A reversal is not applied from a batch: it is a compensating movement on money
                // that already left, and the webhook path carries the detail this record does not.
                return false;
        }
    }

    /// <summary>
    /// Whether a finished withdrawal and the provider's record contradict each other.
    /// <para>
    /// A provider reporting <c>paid</c> against a <c>Completed</c> withdrawal agrees; against a
    /// <c>Failed</c> one it does not, and that is a member debited for a payout the platform
    /// believes never happened.
    /// </para>
    /// </summary>
    private static bool IsDisagreement(PayoutReconciliationRecord record, Withdrawal withdrawal) =>
        record.Status.ToLowerInvariant() switch
        {
            "paid" => withdrawal.Status is WithdrawalStatus.Failed or WithdrawalStatus.Cancelled,
            "failed" => withdrawal.Status == WithdrawalStatus.Completed,
            _ => false
        };

    /// <summary>
    /// Whether the provider's figures match the platform's. The net is compared, because the net is
    /// what the provider was asked to move — the fee never left.
    /// </summary>
    private static bool Agrees(PayoutReconciliationRecord record, Withdrawal withdrawal) =>
        record.AmountMinorUnits == withdrawal.Net.MinorUnits
        && string.Equals(record.Currency, withdrawal.Currency, StringComparison.OrdinalIgnoreCase);

    private static async Task<Withdrawal?> MatchAsync(
        PayoutReconciliationRecord record,
        IWithdrawalRepository withdrawals,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.Reference))
        {
            return null;
        }

        var reference = record.Reference.Trim();

        if (reference.StartsWith("diaspora-payout-", StringComparison.OrdinalIgnoreCase))
        {
            reference = reference["diaspora-payout-".Length..];
        }

        return Guid.TryParse(reference, out var id)
            ? await withdrawals.FindAsync(WithdrawalId.From(id), cancellationToken)
            : null;
    }
}
