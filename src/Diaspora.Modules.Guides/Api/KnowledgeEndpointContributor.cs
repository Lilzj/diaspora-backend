using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The Country Knowledge Directory's HTTP surface.
/// <para>
/// <b>A second endpoint contributor from the same module, which the kernel accepts because
/// contributors are collected as a set.</b> It is a separate class because it maps a separate URL
/// space for a separate audience: <c>/api/v1/guides</c> is where a guide is written and read, and
/// <c>/api/v1/knowledge</c> is where a country's knowledge is discovered and curated. Folding them
/// into one contributor would have produced a file mapping two products.
/// </para>
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs, and returns through
/// <c>PlatformResults</c> so the envelope and problem documents are identical to every other
/// module's. Nothing here is known to the host: <c>Program.cs</c> does not name this module.
/// </para>
/// </summary>
public sealed class KnowledgeEndpointContributor : IEndpointContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(GuidesModuleMetadata.KnowledgeRoutePrefix)
            .WithTags("Knowledge");

        KnowledgeDiscoveryEndpoints.Map(group);
        KnowledgeCatalogueEndpoints.Map(group);
        KnowledgeDirectoryEndpoints.Map(group);
        KnowledgeCurationEndpoints.Map(group);
        KnowledgeLearningPathEndpoints.Map(group);
        KnowledgeAnalyticsEndpoints.Map(group);
    }
}
