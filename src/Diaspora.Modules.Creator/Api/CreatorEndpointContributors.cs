using Diaspora.Platform.Modules.Contributors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Creator.Api;

/// <summary>
/// Maps the creator projection's URL space: <c>/api/v1/creators</c>.
/// <para>
/// <b>Route order matters and is not incidental.</b> Literal segments are mapped before their
/// parameterised siblings throughout — <c>/creators/me</c> and <c>/creators/leaderboard</c> before
/// <c>/creators/{creatorId:guid}</c>. The <c>:guid</c> constraint is what stops a literal being
/// swallowed, and the ordering is what makes it readable.
/// </para>
/// <para>
/// <b>Six contributors rather than one, because the register asks for six URL spaces.</b> B8
/// established the shape: one class per prefix, all discovered as a set. See
/// <c>CreatorModuleMetadata</c> for why the prefixes are the client's choice rather than this
/// platform's.
/// </para>
/// </summary>
public sealed class CreatorEndpointContributor : IEndpointContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(CreatorModuleMetadata.CreatorsRoutePrefix)
            .WithTags("Creator");

        CreatorProfileEndpoints.Map(group);
        CreatorVerificationEndpoints.Map(group);
        CreatorMilestoneEndpoints.Map(group);
        CreatorDiscoveryEndpoints.Map(group);
    }
}

/// <summary>Maps <c>/api/v1/challenges</c> — CREATOR-GAP-004.</summary>
public sealed class CreatorChallengeEndpointContributor : IEndpointContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        ChallengeEndpoints.Map(
            endpoints
                .MapGroup(CreatorModuleMetadata.ChallengesRoutePrefix)
                .WithTags("Creator Challenges"));
    }
}

/// <summary>Maps <c>/api/v1/grants</c> — CREATOR-GAP-005.</summary>
public sealed class CreatorGrantEndpointContributor : IEndpointContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        GrantEndpoints.Map(
            endpoints
                .MapGroup(CreatorModuleMetadata.GrantsRoutePrefix)
                .WithTags("Community Grants"));
    }
}

/// <summary>Maps <c>/api/v1/academy</c> — CREATOR-GAP-006.</summary>
public sealed class CreatorAcademyEndpointContributor : IEndpointContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        AcademyEndpoints.Map(
            endpoints
                .MapGroup(CreatorModuleMetadata.AcademyRoutePrefix)
                .WithTags("Creator Academy"));
    }
}

/// <summary>Maps <c>/api/v1/referrals</c> — CREATOR-GAP-009.</summary>
public sealed class CreatorReferralEndpointContributor : IEndpointContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        ReferralEndpoints.Map(
            endpoints
                .MapGroup(CreatorModuleMetadata.ReferralsRoutePrefix)
                .WithTags("Creator Referrals"));
    }
}

/// <summary>
/// Maps <c>/api/v1/rewards</c> — CREATOR-GAP-010 and -011.
/// <para>
/// <b>Not a points balance.</b> B15 serves that at <c>/api/v1/financial/rewards/me</c> and nothing
/// under this prefix returns one. The two spaces do not collide, and the separation is the boundary
/// made visible in the URL.
/// </para>
/// </summary>
public sealed class CreatorRewardEndpointContributor : IEndpointContributor
{
    public string Module => CreatorModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        RewardEndpoints.Map(
            endpoints
                .MapGroup(CreatorModuleMetadata.RewardsRoutePrefix)
                .WithTags("Creator Rewards"));
    }
}
