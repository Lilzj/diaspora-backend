using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Hashtags;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Observability;
using Diaspora.Modules.Community.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Community.Infrastructure.Hosting;

/// <summary>
/// Expires posts past their lifetime and rolls the trending window.
/// <para>
/// Two jobs in one timer because they run on the same cadence and both are cheap. Expiry is the
/// mechanism that makes a hyperlocal feed trustworthy: a traffic alert that outlives the traffic
/// teaches members the feed is stale, and they stop reading the ones that matter.
/// </para>
/// <para>
/// The module registers its own hosted service because it cannot edit <c>Program.cs</c>. That is
/// the contributor model working as intended, and the same mechanism Location uses for its
/// catalogue seeder and Media for its sweeper.
/// </para>
/// <para>
/// Batched, so one pass over a backlog does not hold a connection open indefinitely. A failure
/// is logged and the loop continues — a sweep that dies on one bad row and never runs again is
/// worse than one that skips it.
/// </para>
/// </summary>
public sealed class CommunitySweeper : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CommunityOptions _options;
    private readonly ILogger<CommunitySweeper> _logger;

    public CommunitySweeper(
        IServiceScopeFactory scopeFactory,
        IOptions<CommunityOptions> options,
        ILogger<CommunitySweeper> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options?.Value ?? new CommunityOptions();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnableExpirySweep)
        {
            _logger.LogInformation("CommunitySweeper is disabled by configuration.");

            return;
        }

        var interval = TimeSpan.FromMinutes(
            Math.Max(1, _options.ExpirySweepIntervalMinutes));

        // A minute of grace so the sweep does not compete with startup.
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        using var timer = new PeriodicTimer(interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
#pragma warning disable CA1031 // A failed pass must not stop every future pass.
            catch (Exception exception)
            {
                _logger.LogError(exception, "The community sweep failed. It will run again.");
            }
#pragma warning restore CA1031
        }
    }

    /// <summary>Public so a test can run one pass without waiting for a timer.</summary>
    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var posts = scope.ServiceProvider.GetRequiredService<ICommunityPostRepository>();
        var hashtags = scope.ServiceProvider.GetRequiredService<IHashtagRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = clock.UtcNow;

        var expired = await posts.ListExpiredAsync(
            now, Math.Max(1, _options.ExpirySweepBatchSize), cancellationToken);

        foreach (var post in expired)
        {
            post.Expire(now);
        }

        if (expired.Count > 0)
        {
            CommunityTelemetry.PostsExpired.Add(expired.Count);

            _logger.LogInformation("Expired {Count} community posts.", expired.Count);
        }

        var stale = await hashtags.ListForWindowRollAsync(
            Math.Max(1, _options.TrendingRollBatchSize), cancellationToken);

        var cutoff = now.AddHours(-Math.Max(1, _options.TrendingWindowHours));

        foreach (var hashtag in stale.Where(hashtag => hashtag.LastUsedAt < cutoff))
        {
            hashtag.RollWindow();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
