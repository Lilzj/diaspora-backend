using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;

namespace Diaspora.Modules.Creator.Application.Services;

/// <summary>
/// The badges a creator has earned, read from claimed milestones.
/// <para>
/// <b>Not a badge system.</b> The register notes that <c>ALL_CREATOR_BADGES</c> in the client is "a
/// hard-coded badge shelf a real contract would replace"; this is that contract, and it is derived
/// rather than stored. A badge is exactly "a milestone this creator claimed whose definition grants
/// one" — so there is no second catalogue to keep in step with the milestone catalogue, and no way
/// for the two to disagree.
/// </para>
/// <para>
/// <b>One query for a whole page.</b> A directory of twenty creators reads badges once rather than
/// twenty times, which is what keeps the badge list on the public projection affordable enough to be
/// consistent between the directory and the single profile.
/// </para>
/// </summary>
public sealed class CreatorBadgeReader
{
    private readonly ICreatorMilestoneProgressRepository _progress;
    private readonly ICreatorMilestoneRepository _milestones;

    public CreatorBadgeReader(
        ICreatorMilestoneProgressRepository progress, ICreatorMilestoneRepository milestones)
    {
        _progress = progress;
        _milestones = milestones;
    }

    /// <summary>Badges for several creators at once, keyed by creator.</summary>
    public async Task<IReadOnlyDictionary<CreatorId, IReadOnlyList<CreatorBadgeResponse>>> ForAsync(
        IReadOnlyCollection<CreatorId> creatorIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorIds);

        var result = new Dictionary<CreatorId, IReadOnlyList<CreatorBadgeResponse>>();

        if (creatorIds.Count == 0)
        {
            return result;
        }

        var definitions = (await _milestones.ListActiveAsync(cancellationToken))
            .Where(definition => !string.IsNullOrWhiteSpace(definition.RewardBadgeKey))
            .ToDictionary(definition => definition.Id);

        if (definitions.Count == 0)
        {
            foreach (var creatorId in creatorIds)
            {
                result[creatorId] = [];
            }

            return result;
        }

        foreach (var creatorId in creatorIds.Distinct())
        {
            var claimed = await _progress.ListForCreatorAsync(creatorId, cancellationToken);

            result[creatorId] = claimed
                .Where(row =>
                    row.State == CreatorMilestoneState.Claimed
                    && definitions.ContainsKey(row.MilestoneId))
                .Select(row =>
                {
                    var definition = definitions[row.MilestoneId];

                    return new CreatorBadgeResponse
                    {
                        BadgeId = definition.Id.Value.ToString(),
                        Key = definition.RewardBadgeKey!,
                        Label = definition.Title,
                        Description = definition.Description,
                        Icon = definition.Icon,
                        IsEarned = true,
                        EarnedAt = row.ClaimedAt
                    };
                })
                .ToList();
        }

        return result;
    }

    /// <summary>Badges for one creator.</summary>
    public async Task<IReadOnlyList<CreatorBadgeResponse>> ForAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        var map = await ForAsync([creatorId], cancellationToken);

        return map.TryGetValue(creatorId, out var badges) ? badges : [];
    }
}
