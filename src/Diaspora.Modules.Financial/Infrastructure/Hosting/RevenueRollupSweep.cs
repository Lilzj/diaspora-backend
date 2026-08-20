using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Revenue;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Recomputes yesterday's revenue, per currency.
/// <para>
/// <b>Recomputed rather than accumulated, so the table is disposable.</b> Delete every row and the
/// next run reproduces it; a late refund arriving after a period closed corrects the row rather than
/// adding a second one beside it. Nothing else in this module reads a revenue row to make a decision.
/// </para>
/// <para>
/// <b>It runs after the placement sweep.</b> The ordering is a design decision and is stated in the
/// documentation rather than left to two timers — a rollup that ran first would count a placement the
/// featured rail has already dropped.
/// </para>
/// <para>
/// <b>One row per currency and no total across them.</b> Producing one would need a rate; the
/// platform does not convert.
/// </para>
/// </summary>
public sealed class RevenueRollupSweep : FinancialSweep
{
    public RevenueRollupSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<RevenueRollupSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableRevenueRollupSweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.RevenueRollupSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var transactions = scope.ServiceProvider.GetRequiredService<IFinancialTransactionRepository>();
        var revenue = scope.ServiceProvider.GetRequiredService<IRevenueRecordRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var periodEnd = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var periodStart = periodEnd.AddDays(-1);

        var completed = (await transactions.ListForPeriodAsync(
                periodStart, periodEnd, cancellationToken))
            .Where(transaction => transaction.Status == TransactionStatus.Completed)
            .ToList();

        var handled = 0;

        foreach (var byCurrency in completed.GroupBy(
            transaction => transaction.Currency, StringComparer.Ordinal))
        {
            var gross = byCurrency
                .Where(transaction => transaction.TransactionType is TransactionType.Payment
                    or TransactionType.Purchase
                    or TransactionType.Subscription)
                .Sum(transaction => transaction.AmountMinorUnits);

            var commission = byCurrency
                .Where(transaction => transaction.TransactionType == TransactionType.Commission)
                .Sum(transaction => transaction.AmountMinorUnits);

            var fees = byCurrency
                .Where(transaction => transaction.TransactionType == TransactionType.Fee)
                .Sum(transaction => transaction.AmountMinorUnits);

            var refunds = byCurrency
                .Where(transaction => transaction.TransactionType == TransactionType.Refund)
                .Sum(transaction => transaction.AmountMinorUnits);

            var record = await revenue.FindForPeriodAsync(
                periodStart, RevenueDimension.Total, "*", byCurrency.Key, cancellationToken);

            if (record is null)
            {
                record = RevenueRecord.ForPeriod(
                    RevenueRecordId.From(guids.NewSequentialGuid()),
                    periodStart,
                    periodEnd,
                    RevenueDimension.Total,
                    "*",
                    byCurrency.Key,
                    now);

                revenue.Add(record);
            }
            else
            {
                revenue.Update(record);
            }

            // Gross counts what came in; the platform's own share is commission plus fees less
            // refunds. Storing all five separately is what makes "what did refunds cost us" a query
            // rather than a replay of the ledger.
            record.Record(gross, commission, fees, refunds, byCurrency.Count(), now);
            handled++;
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }
}
