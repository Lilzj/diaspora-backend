using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Ranking;
using Diaspora.Modules.Guides.Extensibility.Verification;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Capabilities;

/// <summary>
/// Describes the deployment.
/// <para>
/// Everything here is composed from registries and domain constants rather than hard-coded, so a
/// deployment that contributes a ranking strategy or installs a verification provider sees it
/// appear in the client without an app release. That is the whole reason the endpoint exists —
/// a hard-coded copy of this payload in the client would be a second source of truth that goes
/// stale silently.
/// </para>
/// <para>
/// The sort keys are emitted through <c>GuideWireNames</c> rather than as enum names, because
/// they travel in query strings and the wire contract pins them lower-case kebab. Emitting two
/// spellings of one value is how every client that compares string literals breaks.
/// </para>
/// <para>
/// The category catalogue is deliberately <em>not</em> inlined here: it is a database read that
/// changes on its own schedule and has its own cacheable endpoint, and folding it in would make
/// a static description of the software depend on a table.
/// </para>
/// </summary>
public sealed class GetGuideCapabilitiesQueryHandler
    : IQueryHandler<GetGuideCapabilitiesQuery, GuideCapabilitiesResponse>
{
    private readonly GuideRankingStrategyRegistry _rankings;
    private readonly GuideVerificationProviderRegistry _verification;

    public GetGuideCapabilitiesQueryHandler(
        GuideRankingStrategyRegistry rankings,
        GuideVerificationProviderRegistry verification)
    {
        _rankings = rankings;
        _verification = verification;
    }

    public Task<Result<GuideCapabilitiesResponse>> Handle(
        GetGuideCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var response = new GuideCapabilitiesResponse
        {
            Statuses = Names<GuideStatus>(),
            Visibilities = Names<GuideVisibility>(),
            BlockKinds = Names<GuideBlockKind>(),
            Difficulties = Names<GuideDifficulty>(),
            MediaRoles = Names<GuideMediaRole>(),
            VoteKinds = Names<GuideVoteKind>(),
            SortOrders = Enum.GetValues<GuideSortOrder>()
                .Select(GuideWireNames.ToWire)
                .ToList(),
            RankingKeys = _rankings.Keys.ToList(),
            VerificationAvailable = _verification.CanSubmit,
            MaxSections = GuideLimits.MaxSections,
            MaxBlocksPerSection = GuideLimits.MaxBlocksPerSection,
            MaxBlockLength = GuideLimits.MaxBlockLength,
            MaxTags = GuideLimits.MaxTags,
            MaxMediaReferences = GuideLimits.MaxMediaReferences,
            MaxProviderReferences = GuideLimits.MaxProviderReferences,
            MaxSummaryLength = GuideLimits.MaxSummary,
            MaxPageSize = GuideLimits.MaxPageSize
        };

        return Task.FromResult(Result.Success(response));
    }

    private static string[] Names<TEnum>() where TEnum : struct, Enum => Enum.GetNames<TEnum>();
}
