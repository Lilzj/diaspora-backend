using Diaspora.Modules.Notifications.Options;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Notifications.Api;

/// <summary>
/// Maps B13's public URL space.
/// <para>
/// One group, one prefix, declared once in the module's metadata and asserted by an architecture
/// test — so a second surface cannot appear elsewhere in the URL space without somebody noticing.
/// </para>
/// <para>
/// <b>The hub is mapped on the root builder rather than inside the group</b>, because it lives
/// outside the versioned API space: a socket has no representation to version, and
/// <c>/hubs/realtime</c> is a platform-wide prefix the host authenticates by query-string token. See
/// <see cref="NotificationsModuleMetadata.RealtimeHubPath"/>.
/// </para>
/// <para>
/// <b>Mapping a hub from a module contributor is the whole claim of this change.</b> The real-time
/// runtime arrives with no edit to <c>Program.cs</c>, no edit to the kernel and no edit to any other
/// module — the same terms every vertical since B3 has plugged in on. <c>AddSignalR</c> is called
/// from the module's own <c>ConfigureServices</c>; this is the other half.
/// </para>
/// </summary>
public sealed class NotificationEndpointContributor : IEndpointContributor
{
    public string Module => NotificationsModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(NotificationsModuleMetadata.RoutePrefix)
            .WithTags("Notifications");

        InboxEndpoints.Map(group);
        NotificationPreferenceEndpoints.Map(group);
        NotificationDeviceEndpoints.Map(group);
        NotificationDigestEndpoints.Map(group);
        NotificationTemplateEndpoints.Map(group);
        NotificationOperationsEndpoints.Map(group);
        RealtimeEndpoints.Map(group);

        MapRealtimeHub(endpoints);
    }

    /// <summary>
    /// Maps the hub, unless the deployment has switched real-time off.
    /// <para>
    /// <b>Not mapped rather than mapped-and-inert.</b> A hub that accepts a socket and then never
    /// sends anything is indistinguishable, from a client, from a hub that is broken — so a client
    /// reconnects forever against a deployment that is working exactly as configured. An absent
    /// route produces a 404 the client can act on, and matches what
    /// <c>/api/v1/notifications/realtime/configuration</c> already told it.
    /// </para>
    /// </summary>
    private static void MapRealtimeHub(IEndpointRouteBuilder endpoints)
    {
        var options = endpoints.ServiceProvider
            .GetService<IOptions<RealtimeOptions>>()?.Value ?? new RealtimeOptions();

        if (!options.Enabled)
        {
            return;
        }

        // No .WithName(). MapHub registers *two* endpoints — the hub itself and its negotiate
        // route — and an endpoint name must be globally unique, so naming the result names both
        // and the router refuses to build. It fails on the first request rather than at map time,
        // which means every test in the suite dies with "duplicate endpoint name" and none of them
        // mentions real-time. Every other Map* call in this module takes a name; this one cannot.
        endpoints.MapHub<RealtimeHub>(NotificationsModuleMetadata.RealtimeHubPath);
    }
}
