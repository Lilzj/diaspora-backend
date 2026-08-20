using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Infrastructure.Realtime;

/// <summary>
/// The shipped backplane: none.
/// <para>
/// <b>Registered rather than absent, and that distinction is the same one B13 made for push.</b> A
/// missing registration would mean nothing could ask what the backplane is, and the question that
/// matters — "is this deployment scaled beyond what its fan-out can reach" — would have nowhere to
/// be asked. With this in place the startup validator asks it on every boot and the health surface
/// reports the answer.
/// </para>
/// <para>
/// <b>It reports what configuration says, not what it has done.</b> A real backplane is wired in
/// beneath the hub by an adapter the deployment installs; this class never sees a message. Its whole
/// job is to answer honestly, which for a single-container deployment is "None, not distributed, and
/// that is fine".
/// </para>
/// <para>
/// Reading <see cref="RealtimeBackplaneOptions.Provider"/> rather than hard-coding <c>None</c> means
/// a deployment that has installed an adapter and said so gets a truthful health report from the
/// default implementation, instead of having to replace this class to stop being told it is
/// single-node.
/// </para>
/// </summary>
public sealed class SingleNodeRealtimeBackplane : IRealtimeBackplane
{
    private readonly IOptionsMonitor<RealtimeOptions> _options;

    public SingleNodeRealtimeBackplane(IOptionsMonitor<RealtimeOptions> options) => _options = options;

    public string Provider
    {
        get
        {
            var declared = _options.CurrentValue.Backplane.Provider;

            return string.IsNullOrWhiteSpace(declared) ? "None" : declared.Trim();
        }
    }

    public bool IsDistributed =>
        !string.Equals(Provider, "None", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True without doing anything.
    /// <para>
    /// The question is "can this process reach the sockets it is holding", and for a single node the
    /// answer is yes as long as the process is running — which, if this is executing, it is. An
    /// adapter that genuinely talks to a broker overrides this and pings it.
    /// </para>
    /// </summary>
    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
