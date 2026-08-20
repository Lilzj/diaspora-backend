using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Observability;
using Diaspora.Modules.Media.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Media.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Media.Infrastructure.Hosting;

/// <summary>
/// Collects what uploads leave behind.
/// <para>
/// <b>The component every hand-rolled upload endpoint discovers it needed, eighteen months
/// late.</b> Three populations accumulate silently, and none of them shows up in any metric
/// that anyone watches, because every request involved succeeded:
/// </para>
/// <list type="number">
/// <item><b>Expired sessions.</b> A member starts a 200 MB upload on a train, loses signal at
/// 60%, and never comes back. The parts sit in storage forever.</item>
/// <item><b>Orphaned assets.</b> A vertical uploads a photograph, then fails to save the
/// listing that would have owned it. The photograph is stored, valid, processed and referenced
/// by nothing.</item>
/// <item><b>Deleted assets.</b> Soft-deleted rows whose bytes are still paid for.</item>
/// </list>
/// <para>
/// The module registers this itself, from <c>ConfigureServices</c>, because it cannot edit
/// <c>Program.cs</c> — the contributor model working as intended rather than a hole in it, and
/// the same mechanism the Location module uses for its catalogue seeder.
/// </para>
/// <para>
/// Storage first, row second, everywhere. A row removed before its bytes leaves an object
/// nothing will ever look for again.
/// </para>
/// </summary>
public sealed class MediaSweeper : BackgroundService
{
    /// <summary>
    /// How many of each population one pass handles. Bounded so a sweep that finds a backlog
    /// of fifty thousand orphans does not hold a database connection for an hour — it takes a
    /// bite, and the next pass takes the next.
    /// </summary>
    private const int BatchSize = 100;

    private readonly IServiceScopeFactory _scopes;
    private readonly MediaUploadOptions _options;
    private readonly ILogger<MediaSweeper> _logger;

    public MediaSweeper(
        IServiceScopeFactory scopes,
        IOptions<MediaUploadOptions> options,
        ILogger<MediaSweeper> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _scopes = scopes;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableSweeper)
        {
            _logger.LogInformation("MediaSweeper is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, _options.SweepIntervalMinutes));

        // A short delay before the first pass so the sweep does not compete with startup —
        // migrations, seeding and the first burst of traffic all land in the same window.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // A failed sweep must not kill the loop. The next pass tries again, and the
                // populations it collects are not urgent — they are expensive, not broken.
                _logger.LogError(exception, "The media sweep failed; it will run again.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    /// <summary>One pass over all three populations. Public so a test can drive it directly.</summary>
    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopes.CreateScope();

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var storage = scope.ServiceProvider.GetRequiredService<StorageProviderRegistry>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = clock.UtcNow;

        await SweepExpiredSessionsAsync(scope, storage, now, cancellationToken);
        await SweepOrphanedAssetsAsync(scope, storage, now, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SweepExpiredSessionsAsync(
        IServiceScope scope,
        StorageProviderRegistry storage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var sessions = scope.ServiceProvider.GetRequiredService<IUploadSessionRepository>();
        var expired = await sessions.FindExpiredAsync(now, BatchSize, cancellationToken);

        foreach (var session in expired)
        {
            var provider = storage.Resolve(session.StorageProvider);

            if (provider.IsSuccess)
            {
                await provider.Value.DeletePrefixAsync(
                    session.StorageContainer,
                    StorageKey.UploadPrefix(session.Id.Value),
                    cancellationToken);
            }

            session.Expire();
        }

        if (expired.Count > 0)
        {
            _logger.LogInformation(
                "Swept {Count} expired upload session(s) and their staged parts.", expired.Count);
        }
    }

    /// <summary>
    /// Removes assets nothing ever claimed.
    /// <para>
    /// The grace period is deliberately longer than the session lifetime. A member who uploads
    /// photographs, closes the app and finishes the listing the next morning must not find them
    /// gone — every composer in the client persists a draft and expects precisely that.
    /// </para>
    /// </summary>
    private async Task SweepOrphanedAssetsAsync(
        IServiceScope scope,
        StorageProviderRegistry storage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var assets = scope.ServiceProvider.GetRequiredService<IMediaAssetRepository>();

        var cutoff = now.AddHours(-Math.Max(1, _options.OrphanGraceHours));
        var orphans = await assets.FindOrphansAsync(cutoff, BatchSize, cancellationToken);

        foreach (var asset in orphans)
        {
            var provider = storage.Resolve(asset.Location.Provider);

            if (provider.IsSuccess)
            {
                // The prefix, not the key: one call removes the original and every rendition.
                await provider.Value.DeletePrefixAsync(
                    asset.Location.Container, asset.Location.Key.Prefix, cancellationToken);
            }

            assets.Remove(asset);
        }

        if (orphans.Count > 0)
        {
            MediaTelemetry.OrphansCollected.Add(orphans.Count);

            // Logged at warning rather than information: a steady stream of orphans means a
            // vertical is uploading and forgetting to attach, which is a bug in that vertical
            // and is invisible in every other signal.
            _logger.LogWarning(
                "Collected {Count} unclaimed media asset(s) older than {Hours}h. A rising " +
                "count means a module is uploading without attaching.",
                orphans.Count, _options.OrphanGraceHours);
        }
    }
}
