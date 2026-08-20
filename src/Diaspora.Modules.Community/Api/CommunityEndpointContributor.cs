using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>
/// The module's HTTP surface, mapped as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs, and returns
/// through <c>PlatformResults</c> so the envelope and problem documents are byte-for-byte
/// identical to Identity's controllers and to Trust's, Location's and Media's endpoints.
/// Nothing here is known to the host.
/// </para>
/// <para>
/// Split by concern rather than gathered into one method, because the module has thirty-four
/// routes and a single <c>MapEndpoints</c> holding all of them is the file nobody reads before
/// adding the thirty-fifth.
/// </para>
/// </summary>
public sealed class CommunityEndpointContributor : IEndpointContributor
{
    public string Module => CommunityModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(CommunityModuleMetadata.RoutePrefix)
            .WithTags("Community");

        PostEndpoints.Map(group);
        CommentEndpoints.Map(group);
        ReactionEndpoints.Map(group);
        FeedEndpoints.Map(group);
        BookmarkEndpoints.Map(group);
        SocialEndpoints.Map(group);
        ModerationEndpoints.Map(group);
        DiscoveryEndpoints.Map(group);
    }
}
