namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// Every reaction on one item.
/// <para>
/// Carries the per-kind breakdown <b>and</b> the three named counts the client already renders:
/// <c>helpfulCount</c>, <c>confirmationCount</c> and <c>likeCount</c>. The named fields are
/// projections of the breakdown, not separate storage — they exist so an existing renderer keeps
/// working, and the breakdown exists so a new reaction kind needs no new field.
/// </para>
/// </summary>
public sealed record ReactionSummaryResponse
{
    public required int TotalCount { get; init; }

    public required IReadOnlyList<ReactionCountResponse> Reactions { get; init; }

    public required IReadOnlyList<string> ViewerReactions { get; init; }

    public required int LikeCount { get; init; }

    public required int HelpfulCount { get; init; }

    public required int ConfirmationCount { get; init; }

    public required int DisputeCount { get; init; }
}
