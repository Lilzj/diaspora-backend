using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>
/// The module's HTTP surface, mapped as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs — validation,
/// telemetry, transactions, authorisation and audit — and returns through <c>PlatformResults</c> so
/// the envelope and problem documents are byte-for-byte identical to every other module's. Nothing
/// here is known to the host: <c>Program.cs</c> does not name this module, and the kernel finds this
/// contributor by scanning loaded assemblies.
/// </para>
/// <para>
/// One group, from the module's single declared prefix, split by concern into files that each
/// answer for one part of the marketplace — because a single <c>MapEndpoints</c> holding sixty
/// routes is the file nobody reads before adding the sixty-first.
/// </para>
/// </summary>
public sealed class MarketplaceEndpointContributor : IEndpointContributor
{
    public string Module => MarketplaceModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(MarketplaceModuleMetadata.RoutePrefix)
            .WithTags("Marketplace");

        MarketplaceDiscoveryEndpoints.Map(group);
        MarketplaceListingEndpoints.Map(group);
        MarketplaceSellerEndpoints.Map(group);
        MarketplaceCommerceEndpoints.Map(group);
        MarketplaceCatalogEndpoints.Map(group);
        MarketplaceModerationEndpoints.Map(group);
        MarketplaceReviewEndpoints.Map(group);
    }
}
