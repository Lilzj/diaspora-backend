using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Extensibility.Advertising;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityProgramme.Infrastructure.Advertising;

/// <summary>
/// The communities an advertiser can target, contributed to B16 — MON-GAP-007.
/// <para>
/// <b>B18 answers because B18 owns the vocabulary.</b> The Financial module references no other
/// module, so it defines the shape and whoever holds the data implements it. This is the same
/// direction as every other contribution on the platform, and it is the reason a targeting picker
/// can offer real communities without Financial knowing what a community is.
/// </para>
/// <para>
/// <b>Only public, live communities are offered.</b> The directory's own visibility rule, applied
/// again here rather than assumed: a picker that listed private communities would tell an advertiser
/// they exist, which is the one thing the visibility model is for.
/// </para>
/// </summary>
public sealed class CommunityTargetingOptionProvider : IAdTargetingOptionProvider
{
    private readonly ICommunityRepository _communities;

    public CommunityTargetingOptionProvider(ICommunityRepository communities) =>
        _communities = communities;

    public AdTargetingDimension Dimension => AdTargetingDimension.Community;

    public async Task<IReadOnlyList<AdTargetingOption>> GetOptionsAsync(
        string? query, int take, CancellationToken cancellationToken = default)
    {
        // SearchAsync already returns public, live communities only — that filter lives in the
        // repository precisely so no caller can forget it.
        var page = await _communities.SearchAsync(
            new CommunityDirectoryFilter { Query = query, Limit = take }, cancellationToken);

        return
        [
            .. page.Items.Select(community => new AdTargetingOption(
                community.Id.Value.ToString(),
                community.Name))
        ];
    }
}

/// <summary>
/// How many people a community selection could reach — MON-GAP-007.
/// <para>
/// <b>A range, because a union of overlapping communities has no single answer.</b> Somebody in
/// three of the selected communities is one person, and the platform cannot tell how many are in
/// more than one. The lower bound is the largest single community — the fewest people the union can
/// contain — and the upper is the sum.
/// </para>
/// <para>
/// <b>The count is maintained, not measured, and the basis says so.</b> §31 forbids implying a
/// popularity the backend does not authoritatively report, which is why the directory refuses to
/// <em>sort</em> by this number. Quoting it as a bound with its provenance attached is a different
/// act from ranking by it: the advertiser is told what it is and can judge it.
/// </para>
/// </summary>
public sealed class CommunityAudienceSizer : IAdAudienceSizer
{
    private readonly ICommunityRepository _communities;

    public CommunityAudienceSizer(ICommunityRepository communities) => _communities = communities;

    public AdTargetingDimension Dimension => AdTargetingDimension.Community;

    public async Task<AdAudienceBounds?> EstimateAsync(
        AdAudience audience, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        if (audience.CommunityIds.Count == 0)
        {
            return null;
        }

        var counts = new List<int>(audience.CommunityIds.Count);

        foreach (var id in audience.CommunityIds)
        {
            var community = await _communities.GetAsync(
                CommunityId.From(id), cancellationToken);

            // A community that is not listable is not reachable either, and one that has been
            // deleted between the builder loading it and the estimate running contributes nothing
            // rather than a zero that would drag the lower bound down.
            if (community is { IsListable: true })
            {
                counts.Add(community.MemberCount);
            }
        }

        if (counts.Count == 0)
        {
            return null;
        }

        return new AdAudienceBounds(
            counts.Max(),
            counts.Sum(count => (long)count),
            counts.Count == 1
                ? "Based on the number of members that community reports."
                : "Based on the members those communities report. Somebody in more than one is "
                  + "counted once at the low end and once per community at the high end.");
    }
}
