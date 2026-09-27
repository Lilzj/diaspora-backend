using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// The Location module's HTTP surface, mapped as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs — validation,
/// authorisation, telemetry, transactions — and returns through <c>PlatformResults</c> so the
/// envelope and problem documents are byte-for-byte identical to Identity's controllers and
/// Trust's endpoints. Nothing here is known to the host.
/// </para>
/// <para>
/// <b>Route ownership.</b> Everything hangs off <c>/api/v1/location</c>. The frontend calls a
/// top-level <c>/neighborhood/{id}</c> today; that path is claimed by no module and is not
/// claimed here either — the hub is served under this prefix and resolves the client's
/// existing slug unchanged.
/// </para>
/// <para>
/// The groups are split by concern into separate files rather than one long method, because
/// the module has thirty routes and a single <c>MapEndpoints</c> containing all of them would
/// be the file nobody reads before adding the thirty-first.
/// </para>
/// </summary>
public sealed class LocationEndpointContributor : IEndpointContributor
{
    public string Module => LocationModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(LocationModuleMetadata.RoutePrefix)
            .WithTags("Location");

        CountryEndpoints.Map(group);
        AreaEndpoints.Map(group);
        SearchEndpoints.Map(group);
        PlaceEndpoints.Map(group);
        HyperlocalEndpoints.Map(group);
        ProfileEndpoints.Map(group);
        ExperienceEndpoints.Map(group);
    }
}
