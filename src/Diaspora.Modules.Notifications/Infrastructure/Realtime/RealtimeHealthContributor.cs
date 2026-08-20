using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Platform.Operations.Health;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// Reports what the communication runtime is actually able to do right now.
/// <para>
/// <b>Two checks, and both answer a question an operator asks during an incident rather than one a
/// dashboard asks continuously.</b> "Are members getting live updates on this instance" and "is push
/// configured at all". Neither is a liveness question — a real-time runtime that is switched off is
/// a perfectly healthy instance — so both report on <see cref="HealthDimension.Dependency"/> and
/// neither can take an instance out of rotation.
/// </para>
/// <para>
/// <b>Counts, never identities.</b> The connection count is on the readiness payload, which is
/// reachable by an operator and by anything that can read it; who is connected is not an operational
/// number and does not belong there.
/// </para>
/// <para>
/// <b>Degraded rather than unhealthy for a missing backplane</b>, even though the startup validator
/// refuses to boot on it. The two are not in conflict: the validator is the thing that stops the
/// mistake, and by the time this runs the deployment is already serving traffic — reporting
/// unhealthy would empty the load balancer for a problem that restarting cannot fix.
/// </para>
/// </summary>
public sealed class RealtimeHealthContributor : IModuleHealthContributor
{
    private readonly IRealtimeConnectionRegistry _connections;
    private readonly IRealtimeBackplane _backplane;
    private readonly RealtimeChannelRegistry _channels;
    private readonly IPushNotificationSender _push;
    private readonly IOptionsMonitor<RealtimeOptions> _realtimeOptions;
    private readonly IOptionsMonitor<PushProviderOptions> _pushOptions;

    public RealtimeHealthContributor(
        IRealtimeConnectionRegistry connections,
        IRealtimeBackplane backplane,
        RealtimeChannelRegistry channels,
        IPushNotificationSender push,
        IOptionsMonitor<RealtimeOptions> realtimeOptions,
        IOptionsMonitor<PushProviderOptions> pushOptions)
    {
        _connections = connections;
        _backplane = backplane;
        _channels = channels;
        _push = push;
        _realtimeOptions = realtimeOptions;
        _pushOptions = pushOptions;
    }

    public string Module => NotificationsModuleMetadata.Name;

    public async Task<IReadOnlyList<ModuleHealthResult>> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        return
        [
            await CheckRealtimeAsync(cancellationToken),
            CheckPush()
        ];
    }

    private async Task<ModuleHealthResult> CheckRealtimeAsync(CancellationToken cancellationToken)
    {
        var options = _realtimeOptions.CurrentValue;

        if (!options.Enabled)
        {
            return ModuleHealthResult.Healthy(
                "notifications.realtime",
                Module,
                HealthDimension.Dependency,
                "Real-time delivery is switched off; notifications reach the inbox and push.",
                new Dictionary<string, object> { ["enabled"] = false });
        }

        var connections = await _connections.CountAsync(cancellationToken);
        var backplaneHealthy = await _backplane.IsHealthyAsync(cancellationToken);

        var data = new Dictionary<string, object>
        {
            ["enabled"] = true,
            ["connections"] = connections,
            ["instances"] = options.InstanceCount,
            ["backplane"] = _backplane.Provider,
            ["distributed"] = _backplane.IsDistributed,

            // B13.3. Which channel families this deployment can authorise, which is the question an
            // operator actually has when a member reports that a live update never arrived: a
            // channel with no owner refuses every join, silently and forever, and there is nothing
            // else to look at that would say so.
            ["channels"] = string.Join(", ", _channels.OwnedPrefixes)
        };

        if (!backplaneHealthy)
        {
            return ModuleHealthResult.Degraded(
                "notifications.realtime",
                Module,
                HealthDimension.Dependency,
                $"The '{_backplane.Provider}' real-time backplane is not reachable; live delivery "
                + "is limited to members attached to this instance.",
                data);
        }

        if (options.InstanceCount > 1 && !_backplane.IsDistributed)
        {
            return ModuleHealthResult.Degraded(
                "notifications.realtime",
                Module,
                HealthDimension.Dependency,
                $"{options.InstanceCount} instances are declared with no backplane; live delivery "
                + "reaches only the members attached to this one.",
                data);
        }

        return ModuleHealthResult.Healthy(
            "notifications.realtime",
            Module,
            HealthDimension.Dependency,
            $"Real-time delivery is available; {connections} connection(s) on this instance.",
            data);
    }

    /// <summary>
    /// Whether push can reach anybody.
    /// <para>
    /// <b>Healthy when nothing is configured, and that is the deliberate reading.</b> "No push
    /// provider" is this platform's shipped position; reporting it as degraded would leave every
    /// deployment permanently amber and make the state that actually matters — a provider named but
    /// not registered — indistinguishable from the normal one.
    /// </para>
    /// </summary>
    private ModuleHealthResult CheckPush()
    {
        var options = _pushOptions.CurrentValue;

        var data = new Dictionary<string, object>
        {
            ["provider"] = options.Provider,
            ["configured"] = options.IsConfigured,
            ["transportReady"] = _push.IsConfigured
        };

        if (!options.IsConfigured)
        {
            return ModuleHealthResult.Healthy(
                "notifications.push",
                Module,
                HealthDimension.ExternalProvider,
                "No push provider is configured; push deliveries are recorded as unsupported.",
                data);
        }

        return _push.IsConfigured
            ? ModuleHealthResult.Healthy(
                "notifications.push",
                Module,
                HealthDimension.ExternalProvider,
                $"Push provider '{options.Provider}' is configured.",
                data)
            : ModuleHealthResult.Degraded(
                "notifications.push",
                Module,
                HealthDimension.ExternalProvider,
                $"Push provider '{options.Provider}' is configured but its transport reports that it "
                + "is not ready; every push will be recorded as unsupported.",
                data);
    }
}
