using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Recommendations.Api;

/// <summary>
/// Maps B14's public URL space.
/// <para>
/// One group, one prefix, declared once in the module's metadata and asserted by an architecture
/// test — so a second surface cannot appear elsewhere in the URL space without somebody noticing.
/// </para>
/// </summary>
public sealed class RecommendationEndpointContributor : IEndpointContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(RecommendationsModuleMetadata.RoutePrefix)
            .WithTags("Recommendations");

        FeedEndpoints.Map(group);
        SignalEndpoints.Map(group);
        PersonalizationEndpoints.Map(group);
        RecommendationOperationsEndpoints.Map(group);

        // CREATOR-GAP-013. Composed by the Creator Platform behind a Diaspora.Contracts seam.
        CreatorRecommendationEndpoints.Map(group);
    }
}
