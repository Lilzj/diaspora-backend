using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Marks credit grants that have run out of time.
/// <para>
/// <b>It marks the grant and does not touch the balance.</b> Reclaiming expired credit is a ledger
/// posting with a counterparty and an audit stamp, not a silent decrement — and it is a decision a
/// deployment should make deliberately rather than inherit. The grant records that it lapsed; the
/// reclaiming posting is left for whoever wants one.
/// </para>
/// </summary>
public sealed class CreditExpirySweep : FinancialSweep
{
    public CreditExpirySweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<CreditExpirySweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnableCreditExpirySweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.CreditExpirySweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var grants = scope.ServiceProvider.GetRequiredService<ICreditGrantRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var handled = 0;

        foreach (var grant in await grants.ListExpiredAsync(
            now, Options.SweepBatchSize, cancellationToken))
        {
            grant.MarkExpired(now);
            grants.Update(grant);
            handled++;
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }
}
