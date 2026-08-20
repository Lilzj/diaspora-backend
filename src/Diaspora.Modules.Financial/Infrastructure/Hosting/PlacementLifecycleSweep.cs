using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// Starts placements whose window has opened and ends those whose window has closed.
/// <para>
/// <b>A placement expires by sweep, not by read, and that is a deliberate design decision rather
/// than an implementation convenience.</b> A consumer maintaining a featured rail cannot poll every
/// row it has ever been told about; it needs to be told. The sweep is what turns "the window closed"
/// into a published withdrawal.
/// </para>
/// <para>
/// <b>It runs before the revenue rollup.</b> B12's handbook entry applies: when two background
/// operations touch one number, the ordering is a design decision that belongs in the documentation
/// rather than being an emergent property of two timers. A rollup that ran first would count a
/// placement the rail has already dropped.
/// </para>
/// </summary>
public sealed class PlacementLifecycleSweep : FinancialSweep
{
    public PlacementLifecycleSweep(
        IServiceScopeFactory scopes,
        IOptions<FinancialOptions> options,
        ILogger<PlacementLifecycleSweep> logger)
        : base(scopes, options, logger)
    {
    }

    protected override bool IsEnabled => Options.EnablePlacementSweep;

    protected override TimeSpan Interval => TimeSpan.FromMinutes(Options.PlacementSweepMinutes);

    public override async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = Scopes.CreateScope();

        var placements = scope.ServiceProvider.GetRequiredService<IPlacementRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var handled = 0;

        foreach (var placement in await placements.ListDueAsync(
            now, Options.SweepBatchSize, cancellationToken))
        {
            placement.Activate(now);
            placements.Update(placement);
            handled++;
        }

        foreach (var placement in await placements.ListLapsedAsync(
            now, Options.SweepBatchSize, cancellationToken))
        {
            placement.Expire(now);
            placements.Update(placement);
            handled++;
        }

        if (handled > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return handled;
    }
}
