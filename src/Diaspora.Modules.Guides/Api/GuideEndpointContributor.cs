using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The module's HTTP surface, mapped as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs, and returns
/// through <c>PlatformResults</c> so the envelope and problem documents are identical to every
/// other module's. Nothing here is known to the host: <c>Program.cs</c> does not name this
/// module, and the kernel finds this contributor by scanning loaded assemblies.
/// </para>
/// <para>
/// One group, from the module's single declared prefix, split by concern into files that each
/// answer for one part of the knowledge base — because a single <c>MapEndpoints</c> holding
/// eighty routes is the file nobody reads before adding the eighty-first.
/// </para>
/// </summary>
public sealed class GuideEndpointContributor : IEndpointContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(GuidesModuleMetadata.RoutePrefix)
            .WithTags("Guides");

        GuideEndpoints.Map(group);
        GuideDiscoveryEndpoints.Map(group);
        GuideVersionEndpoints.Map(group);
        GuideEngagementEndpoints.Map(group);
        GuideModerationEndpoints.Map(group);
        GuideCategoryEndpoints.Map(group);
    }
}
