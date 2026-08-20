using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Diaspora.IntegrationTests.Common;

/// <summary>
/// The same host with the real-time runtime switched off.
/// <para>
/// <b>A whole factory for one setting, because that setting takes a different registration path.</b>
/// With real-time disabled the module does not call <c>AddSignalR</c>, does not register the SignalR
/// notifier and does not map the hub — three decisions no other test exercises. The one that
/// actually matters is the notifier: leaving the SignalR implementation registered would leave
/// <c>IHubContext&lt;RealtimeHub&gt;</c> unresolvable behind it, and every in-app delivery on that
/// deployment would throw. Nothing at startup would catch it; the failure would arrive in the
/// delivery sweep, in production, on a deployment whose only sin was setting a flag to false.
/// </para>
/// <para>
/// It also stands behind a claim made in the documentation — that turning real-time off is safe at
/// any time and leaves notifications reaching the inbox and push — which is worth an actual test
/// rather than a sentence.
/// </para>
/// </summary>
public sealed class RealtimeDisabledWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureWebHost(builder);

        // Layered after the base factory's own configuration, so this wins.
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:Realtime:Enabled"] = "false"
            });
        });
    }
}
