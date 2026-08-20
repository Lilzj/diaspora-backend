using Diaspora.Modules.Media.Application.Capabilities;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Media.Api;

/// <summary>
/// What this deployment can actually do.
/// <para>
/// One route, and it saves the client from hardcoding four things it cannot know: the size
/// ceilings, the chunk size, which variants exist here, and whether uploads are scanned. A
/// client that assumes them is a client that lets a member upload four hundred megabytes
/// before finding out.
/// </para>
/// </summary>
internal static class MediaCapabilityEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/capabilities", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetMediaCapabilitiesQuery(), cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(MediaPolicies.Read)
            .WithName("GetMediaCapabilities")
            .WithSummary("Limits, accepted formats and available processing for this deployment.");
    }
}
