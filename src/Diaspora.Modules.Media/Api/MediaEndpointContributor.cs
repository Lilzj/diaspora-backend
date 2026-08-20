using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Media.Api;

/// <summary>
/// The Media module's HTTP surface, mapped as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs — validation,
/// authorisation, telemetry, transactions — and returns through <c>PlatformResults</c> so the
/// envelope and problem documents are byte-for-byte identical to Identity's controllers,
/// Trust's endpoints and Location's. Nothing here is known to the host.
/// </para>
/// <para>
/// The one deliberate exception is the content endpoint, which returns a stream rather than
/// the JSON envelope. That is not an inconsistency: the envelope describes a resource, and
/// this route <em>is</em> the bytes. Wrapping a 512 MB video in a JSON body would be absurd,
/// and an <c>&lt;img src&gt;</c> cannot parse one.
/// </para>
/// <para>
/// Split by concern into separate files rather than one long method, because the module has
/// twenty routes and a single <c>MapEndpoints</c> containing all of them would be the file
/// nobody reads before adding the twenty-first.
/// </para>
/// </summary>
public sealed class MediaEndpointContributor : IEndpointContributor
{
    public string Module => MediaModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(MediaModuleMetadata.RoutePrefix)
            .WithTags("Media");

        UploadEndpoints.Map(group);
        MediaAssetEndpoints.Map(group);
        MediaContentEndpoints.Map(group);
        MediaCollectionEndpoints.Map(group);
        MediaProcessingEndpoints.Map(group);
        MediaCapabilityEndpoints.Map(group);
    }
}
