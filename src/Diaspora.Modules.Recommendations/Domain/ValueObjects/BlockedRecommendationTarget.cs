using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// One thing the member said never to show them again.
/// <para>
/// <b>Distinct from a dismissal, and the difference is permanence.</b> A dismissal is "not now" and
/// expires; a block is "not ever" and does not. Merging them would mean either dismissals never
/// expiring — so a member who swiped away one flat on a bad day is denied it forever — or blocks
/// expiring, which would quietly re-show somebody the seller they blocked.
/// </para>
/// <para>
/// <b>The block lives here rather than in Community's block list, and both are correct.</b>
/// Community owns "I have blocked this person"; this owns "I have blocked this listing". The policy
/// that enforces the first consumes Community's published contract; the policy that enforces the
/// second reads this row.
/// </para>
/// </summary>
public sealed class BlockedRecommendationTarget : ValueObject
{
    private BlockedRecommendationTarget(
        Guid blockId, RecommendationTargetType targetType, Guid targetId, DateTimeOffset blockedAt)
    {
        BlockId = blockId;
        TargetType = targetType;
        TargetId = targetId;
        BlockedAt = blockedAt;
    }

    private BlockedRecommendationTarget() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid BlockId { get; private set; }

    public RecommendationTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public DateTimeOffset BlockedAt { get; private set; }

    public static BlockedRecommendationTarget Create(
        Guid blockId,
        RecommendationTargetType targetType,
        Guid targetId,
        DateTimeOffset blockedAt)
    {
        if (targetId == Guid.Empty)
        {
            throw new ArgumentException(
                "A block needs a target. An empty identifier would block nothing while appearing to "
                + "block something.",
                nameof(targetId));
        }

        return new BlockedRecommendationTarget(blockId, targetType, targetId, blockedAt);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BlockId;
        yield return TargetType;
        yield return TargetId;
    }
}
