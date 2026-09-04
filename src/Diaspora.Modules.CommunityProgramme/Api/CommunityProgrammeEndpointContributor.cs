using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityProgramme.Api;

/// <summary>
/// Maps B18's public URL space.
/// <para>
/// One group, one prefix, declared once in the module's metadata and asserted by an architecture
/// test — so a second surface cannot appear elsewhere in the URL space without somebody noticing.
/// </para>
/// <para>
/// <b>Route order matters and is not incidental.</b> Literal segments are mapped before their
/// parameterised siblings throughout: <c>/memberships/me</c> before
/// <c>/memberships/{membershipId:guid}</c>, and <c>/volunteer/applications/me</c> before
/// <c>/volunteer/applications/{applicationId:guid}</c>. The <c>:guid</c> constraint is what stops the
/// literal being swallowed, and the ordering is what makes it readable.
/// </para>
/// </summary>
public sealed class CommunityProgrammeEndpointContributor : IEndpointContributor
{
    public string Module => CommunityProgrammeModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(CommunityProgrammeModuleMetadata.RoutePrefix)
            .WithTags("Community Programme");

        // First, and the order is not cosmetic. `/{idOrSlug}` is a single parameterised segment
        // sitting beside every literal one below it; mapping it here keeps it visible next to what
        // it competes with. ASP.NET Core resolves by specificity rather than by registration order,
        // so the literals still win — CommunitySlug refusing those words is what protects the
        // handles, and a guardrail over the composed route table is what keeps that list honest.
        CommunityEndpoints.Map(group);

        OrganizationEndpoints.Map(group);
        MembershipEndpoints.Map(group);
        VolunteerEndpoints.Map(group);
        PollEndpoints.Map(group);
        AnnouncementEndpoints.Map(group);
        InsightEndpoints.Map(group);

        // B15.1's fundraising surface, at the register's paths. The routes are this module's because
        // it owns the prefix; every decision behind them is B15's. See CampaignEndpoints.
        CampaignEndpoints.Map(group);
    }
}
