using Diaspora.Platform.Operations.Policies;
using Diaspora.Platform.Operations.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Workers;

/// <summary>
/// Runs every registered <see cref="IThreatDetector"/> on a schedule and publishes what they find.
/// <para>
/// Pull rather than push, and on a schedule rather than on a request. A detector on a request path is
/// on that path's latency budget, and the detections worth having — a rate that has changed, a breadth
/// that is unusual, a country that has never appeared before — are comparisons against a window that
/// one event cannot make.
/// </para>
/// <para>
/// <b>A detector that throws does not stop the others.</b> Detectors are the most speculative code the
/// platform runs — heuristics over data whose shape changes — and one of them failing must not silence
/// the rest.
/// </para>
/// </summary>
public sealed class ThreatDetectionWorker : PlatformWorker
{
    private readonly IServiceProvider _services;

    public ThreatDetectionWorker(
        IServiceProvider services,
        IOperationalPolicyCatalog policies,
        ILogger<ThreatDetectionWorker> logger)
        : base("threat-detection", "Platform", policies, logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();

        var detectors = scope.ServiceProvider.GetServices<IThreatDetector>().ToArray();

        if (detectors.Length == 0)
        {
            return 0;
        }

        var sink = scope.ServiceProvider.GetRequiredService<IThreatSignalSink>();
        var published = 0;

        foreach (var detector in detectors)
        {
            IReadOnlyList<ThreatSignal> signals;

            try
            {
                signals = await detector.DetectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, "The {Detector} threat detector threw.", detector.Name);
                continue;
            }

            foreach (var signal in signals)
            {
                await sink.PublishAsync(signal, cancellationToken).ConfigureAwait(false);
                published++;
            }
        }

        // Zero rather than the published count, for the same reason as the maintenance worker: this is
        // a periodic sweep, not a queue, and a busy minute must not make it run continuously.
        return 0;
    }
}
