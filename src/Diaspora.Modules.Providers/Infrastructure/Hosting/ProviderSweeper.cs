using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Providers.Infrastructure.Hosting;

/// <summary>
/// The periodic repair pass.
/// <para>
/// Two jobs, both of which exist because a denormalised value can be wrong in ways nothing else
/// would ever notice.
/// </para>
/// <para>
/// <b>Lapsed verifications.</b> A grant with an expiry stops being current the moment it passes,
/// and the read path already honours that — <c>ProviderVerificationSummary.From</c> checks expiry
/// against now rather than trusting the stored state. But the <em>stored</em> flag is what the
/// <c>verifiedOnly</c> filter reads, and an indexed column cannot evaluate itself. Without this
/// pass, a business whose registration lapsed six months ago keeps appearing in the verified
/// filter forever.
/// </para>
/// <para>
/// <b>Category counts.</b> The chip row renders "Food &amp; Restaurants (128)" from a
/// denormalised count, deliberately, so a ten-chip row is not ten aggregate scans on every page
/// load. Approximate is fine; unboundedly stale is not.
/// </para>
/// <para>
/// Rating rollups are <em>not</em> swept, and that is worth stating: they are recomputed
/// synchronously on every review change, so a sweep would find nothing. A repair path exists
/// (<c>ReviewSummaryService.RefreshManyAsync</c>) for the day something does go wrong, and it is
/// invoked deliberately rather than on a timer.
/// </para>
/// </summary>
public sealed class ProviderSweeper : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ProviderOptions> _options;
    private readonly ILogger<ProviderSweeper> _logger;

    public ProviderSweeper(
        IServiceScopeFactory scopes,
        IOptions<ProviderOptions> options,
        ILogger<ProviderSweeper> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.EnableSweeper)
        {
            return;
        }

        using var timer = new PeriodicTimer(_options.Value.SweepInterval);

        // One pass at startup, then on the interval. A deployment that has been down for a week
        // should not wait another six hours before its verified badges become honest again.
        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
#pragma warning disable CA1031 // One failed pass must not end the sweeper for the process lifetime.
            catch (Exception exception)
            {
                _logger.LogError(exception, "A provider sweep failed. The next pass will retry.");
            }
#pragma warning restore CA1031
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var providers = scope.ServiceProvider.GetRequiredService<IProviderRepository>();
        var categories = scope.ServiceProvider.GetRequiredService<IServiceCategoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var now = clock.UtcNow;

        var lapsed = await providers.ListWithLapsedVerificationsAsync(
            now, _options.Value.SweepBatchSize, cancellationToken);

        foreach (var provider in lapsed)
        {
            provider.RecomputeVerificationSummary(now);
        }

        var counts = await providers.CountByCategoryAsync(cancellationToken);
        var all = await categories.ListAllAsync(includeRetired: true, cancellationToken);

        foreach (var category in all)
        {
            // A subtree count, not a direct one: choosing "Food & Restaurants" in the filter
            // returns everything beneath it, so the number beside it has to mean the same thing.
            // Anything else makes the chip say 4 and the results say 128.
            var subtree = counts
                .Where(entry => IsWithin(entry.Key, category, all))
                .Sum(entry => entry.Value);

            category.SetProviderCount(subtree);
        }

        if (lapsed.Count > 0 || all.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (lapsed.Count > 0)
        {
            _logger.LogInformation(
                "Recomputed verification for {Count} providers whose grants had lapsed.",
                lapsed.Count);
        }
    }

    /// <summary>
    /// Whether a category identifier sits at or beneath a node, by materialised path.
    /// <para>
    /// In memory over a few hundred rows, which is the whole catalogue. A query per category
    /// would be a few hundred round trips to compute numbers that are explicitly allowed to be
    /// approximate.
    /// </para>
    /// </summary>
    private static bool IsWithin(
        Guid candidateId, ServiceCategory root, IReadOnlyList<ServiceCategory> all)
    {
        var candidate = all.FirstOrDefault(category => category.Id.Value == candidateId);

        return candidate is not null && candidate.Path.IsWithin(root.Path);
    }
}
