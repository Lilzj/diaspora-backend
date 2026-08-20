using Diaspora.Modules.Financial.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Infrastructure.Hosting;

/// <summary>
/// The shape every background sweep in this module takes.
/// <para>
/// <b>A public <see cref="RunOnceAsync"/> and a configuration switch, both of which the platform has
/// paid for.</b> A test that asserts on a side effect after a timer happens to have fired is a flaky
/// test wearing a passing badge, and the integration suite shares one SQLite connection that a
/// background loop querying mid-command would be a concurrent use of. Every sweep here can therefore
/// be switched off and driven directly.
/// </para>
/// <para>
/// <b>A sweep never takes the host down.</b> One failed pass is logged and the loop continues; the
/// alternative is a deployment where a transient database error at three in the morning silently
/// stops placements expiring for ever.
/// </para>
/// </summary>
public abstract class FinancialSweep : BackgroundService
{
    protected FinancialSweep(
        IServiceScopeFactory scopes, IOptions<FinancialOptions> options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        Scopes = scopes;
        Options = options.Value;
        Logger = logger;
    }

    protected IServiceScopeFactory Scopes { get; }

    protected FinancialOptions Options { get; }

    protected ILogger Logger { get; }

    /// <summary>Whether this deployment runs the sweep at all.</summary>
    protected abstract bool IsEnabled { get; }

    /// <summary>How often. Read once at start, like every other sweep on the platform.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <summary>One pass. Public so the suite can drive it without waiting for a timer.</summary>
    public abstract Task<int> RunOnceAsync(CancellationToken cancellationToken = default);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            Logger.LogInformation("{Sweep} is disabled by configuration.", GetType().Name);

            return;
        }

        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var handled = await RunOnceAsync(stoppingToken);

                if (handled > 0)
                {
                    Logger.LogInformation("{Sweep} handled {Count} rows.", GetType().Name, handled);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031
            catch (Exception exception)
#pragma warning restore CA1031
            {
                Logger.LogError(exception, "{Sweep} failed a pass and will try again.", GetType().Name);
            }
        }
    }
}
