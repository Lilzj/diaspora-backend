namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>
/// Something the caller never wants to see again.
/// <para>
/// Distinct from a dismissal, which expires. Merging them would mean either dismissals never
/// lapsing — so a member who swiped away one flat on a bad day is denied it forever — or blocks
/// lapsing, which would quietly re-show somebody the seller they blocked.
/// </para>
/// </summary>
public sealed record BlockRecommendationTargetRequest
{
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }
}
