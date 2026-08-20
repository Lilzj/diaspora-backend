using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// Maps B12's public URL space.
/// <para>
/// One group, one prefix. The module's route prefix is declared once in its metadata and asserted by
/// an architecture test, so a second surface cannot appear elsewhere in the URL space without
/// somebody noticing.
/// </para>
/// </summary>
public sealed class CommunityLifeEndpointContributor : IEndpointContributor
{
    public string Module => CommunityLifeModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(CommunityLifeModuleMetadata.RoutePrefix)
            .WithTags("Community Life");

        CommunityLifeDiscoveryEndpoints.Map(group);
        OrganizationEndpoints.Map(group);
        MembershipEndpoints.Map(group);
        EventEndpoints.Map(group);
        EventSeriesEndpoints.Map(group);
        RsvpEndpoints.Map(group);
        CommunityLifeCatalogEndpoints.Map(group);
        CommunityLifeModerationEndpoints.Map(group);
    }
}
