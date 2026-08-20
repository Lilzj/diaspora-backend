using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The Country Intelligence Platform's HTTP surface.
/// <para>
/// <b>A third endpoint contributor from the same module, which the kernel accepts because
/// contributors are collected as a set.</b> It is a separate class because it maps a separate URL
/// space for a separate audience: <c>/api/v1/guides</c> is where a guide is written and read,
/// <c>/api/v1/knowledge</c> is where a country's knowledge is discovered and curated, and
/// <c>/api/v1/intelligence</c> is where official information is published and looked up. Folding them
/// into one contributor would produce a file mapping three products.
/// </para>
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs, and returns through
/// <c>PlatformResults</c> so the envelope and problem documents are identical to every other module's.
/// Nothing here is known to the host: <c>Program.cs</c> does not name this module.
/// </para>
/// </summary>
public sealed class IntelligenceEndpointContributor : IEndpointContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(GuidesModuleMetadata.IntelligenceRoutePrefix)
            .WithTags("Country Intelligence");

        IntelligenceDiscoveryEndpoints.Map(group);
        OfficialNoticeEndpoints.Map(group);
        IntelligenceCatalogueEndpoints.Map(group);
        EmbassyEndpoints.Map(group);
        EmergencyEndpoints.Map(group);
        VisaAdvisoryEndpoints.Map(group);
        PublicHolidayEndpoints.Map(group);
    }
}
