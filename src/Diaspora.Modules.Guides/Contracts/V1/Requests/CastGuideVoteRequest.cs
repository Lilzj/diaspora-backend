namespace Diaspora.Modules.Guides.Contracts.V1.Requests;

/// <summary>
/// The reader's verdict on the guide. One vote per member per guide; casting again with the
/// other kind changes the vote rather than adding one.
/// </summary>
public sealed record CastGuideVoteRequest
{
    /// <summary>A <c>GuideVoteKind</c> name — <c>Helpful</c> or <c>NotHelpful</c>.</summary>
    public required string Kind { get; init; }
}
