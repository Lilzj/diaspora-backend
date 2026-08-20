using System.Collections.Concurrent;
using Diaspora.Operations.Telemetry;
using Diaspora.Platform.Operations.Security;
using Diaspora.Platform.Operations.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Diaspora.Operations.Security;

/// <summary>
/// The sink that ships: counts every signal, logs it at a level matching its severity, keeps a bounded
/// recent window for the dashboard, and hands anything serious to the DDoS hooks.
/// <para>
/// <b>It does not act.</b> No account is locked, no address is blocked, no payment is refused. A
/// detector's output is an observation, and the platform's position is that acting on an observation
/// needs context — a moderation queue, a fraud process, a human — that this layer does not have. A sink
/// that locked accounts would lock a member out of theirs the first time they signed in from abroad.
/// </para>
/// </summary>
public sealed class RecordingThreatSignalSink : IThreatSignalSink
{
    private const int RecentLimit = 500;

    private readonly ConcurrentQueue<ThreatSignal> _recent = new();
    private readonly IServiceProvider _services;
    private readonly ILogger<RecordingThreatSignalSink> _logger;

    public RecordingThreatSignalSink(IServiceProvider services, ILogger<RecordingThreatSignalSink> logger)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(logger);

        _services = services;
        _logger = logger;
    }

    public async Task PublishAsync(ThreatSignal signal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);

        _recent.Enqueue(signal);

        while (_recent.Count > RecentLimit && _recent.TryDequeue(out _))
        {
        }

        OperationsTelemetry.ThreatSignals.Add(1,
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Operation, signal.Kind.ToString()),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Outcome, signal.Severity.ToString()),
            new KeyValuePair<string, object?>(PlatformTelemetryNames.Attributes.Provider, signal.Detector));

        // The subject and the description; never the evidence. Evidence is bounded to counts by
        // contract, but a log line is the widest-read artefact the platform produces and the contract
        // is enforced by review rather than by the compiler.
        Log(signal);

        if (signal.Kind == ThreatKind.TrafficAnomaly && signal.Severity >= ThreatSeverity.High)
        {
            await NotifyMitigationAsync(signal, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The recent window, newest first. For the operational dashboard.</summary>
    public IReadOnlyList<ThreatSignal> Recent(int limit = 100) =>
        [.. _recent.Reverse().Take(limit)];

    private void Log(ThreatSignal signal)
    {
        var level = signal.Severity switch
        {
            ThreatSeverity.Critical => LogLevel.Critical,
            ThreatSeverity.High => LogLevel.Error,
            ThreatSeverity.Medium => LogLevel.Warning,
            ThreatSeverity.Low => LogLevel.Information,
            _ => LogLevel.Debug
        };

#pragma warning disable CA2254 // The template is one of five constants chosen above, not a built string.
        _logger.Log(
            level,
            "Threat signal {Kind} ({Severity}) from {Detector} about {Subject}: {Description}",
            signal.Kind,
            signal.Severity,
            signal.Detector,
            signal.Subject,
            signal.Description);
#pragma warning restore CA2254
    }

    /// <summary>
    /// Hands a serious traffic anomaly to whatever mitigation hooks are registered.
    /// <para>
    /// Resolved lazily rather than injected, because a hook is registered by the deployment and a sink
    /// that took them as a constructor dependency could not be built in a host that has none. A hook
    /// that throws is logged and stepped over: an edge integration failing must not swallow the signal
    /// that reached it.
    /// </para>
    /// </summary>
    private async Task NotifyMitigationAsync(ThreatSignal signal, CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();

        foreach (var hook in scope.ServiceProvider.GetServices<IDdosMitigationHook>())
        {
            try
            {
                await hook.OnAttackSuspectedAsync(signal, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception, "The {Hook} mitigation hook threw while handling a signal.", hook.Name);
            }
        }
    }
}
