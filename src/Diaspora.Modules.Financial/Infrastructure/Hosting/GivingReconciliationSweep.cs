using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Corrects the running totals on causes and funds.
/// <para>
/// <b>Those totals are maintained counters, which means they can drift.</b> A donation refunded
/// after it completed, a redelivered event, a partial failure — each leaves a cause reporting
/// slightly more than it received. The drift is cosmetic by design and bounded by this sweep, which
/// is the same trade B6 recorded for its own counters, made explicit rather than discovered.
/// </para>
/// <para>
/// <b>Only the monetary total is reconciled from money.</b> A supporter count includes everybody who
/// gave anything, and the four non-monetary kinds add to that count and to nothing else.
/// </para>
/// </summary>
public sealed class GivingReconciliationSweep : FinancialSweep
{
    public GivingReconciliationSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<GivingReconciliationSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableGivingReconciliationSweep;

    protected override TimeSpan Interval =>
        TimeSpan.FromMinutes(Options.GivingReconciliationSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var causes = scope.ServiceProvider.GetRequiredService<IDonationCauseRepository>();
        var donations = scope.ServiceProvider.GetRequiredService<IDonationRepository>();
        var funds = scope.ServiceProvider.GetRequiredService<IContributionFundRepository>();
        var contributions =
            scope.ServiceProvider.GetRequiredService<ICommunityContributionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var handled = 0;

        foreach (var cause in await causes.ListAllAsync(cancellationToken))
        {
            var (raised, supporters) = await donations.TotalsForCauseAsync(cause.Id, cancellationToken);

            if (cause.RaisedMinorUnits == raised && cause.SupporterCount == supporters)
            {
                continue;
            }

            cause.ReconcileTotals(raised, supporters, now);
            causes.Update(cause);
            handled++;
        }

        foreach (var fund in await funds.ListAllAsync(cancellationToken))
        {
            var (raised, contributors) =
                await contributions.TotalsForFundAsync(fund.Id, cancellationToken);

            if (fund.RaisedMinorUnits == raised && fund.ContributorCount == contributors)
            {
                continue;
            }

            fund.ReconcileTotals(raised, contributors, now);
            funds.Update(fund);
            handled++;
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }
}
