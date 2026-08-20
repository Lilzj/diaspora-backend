using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Providers.Api;

/// <summary>
/// The module's HTTP surface, mapped as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs, and returns
/// through <c>PlatformResults</c> so the envelope and problem documents are identical to every
/// other module's. Nothing here is known to the host: <c>Program.cs</c> does not name this
/// module, and the kernel finds this contributor by scanning loaded assemblies.
/// </para>
/// <para>
/// Split by concern rather than gathered into one method, because a single
/// <c>MapEndpoints</c> holding every route is the file nobody reads before adding the next one.
/// </para>
/// </summary>
public sealed class ProviderEndpointContributor : IEndpointContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(ProvidersModuleMetadata.RoutePrefix)
            .WithTags("Providers");

        ProviderEndpoints.Map(group);
        ProviderReviewEndpoints.Map(group);
    }
}
