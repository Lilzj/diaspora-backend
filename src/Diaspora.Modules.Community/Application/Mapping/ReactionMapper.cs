using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Integration;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>
/// Projects reaction counts.
/// <para>
/// Emits the per-kind breakdown <b>and</b> the three named counters the client renders today.
/// The named ones are computed from the breakdown rather than stored separately, so a client
/// that has not adopted the breakdown keeps working and one that has is never out of step with
/// it.
/// </para>
/// </summary>
public static class ReactionMapper
{
    public const string Like = "like";
    public const string Helpful = "helpful";
    public const string Confirmed = "confirmed";
    public const string Disputed = "disputed";

    public static ReactionSummaryResponse ToResponse(SocialSummary? summary)
    {
        var counts = summary?.Reactions ?? new Dictionary<string, int>(StringComparer.Ordinal);
        var viewer = summary?.ViewerReactions ?? [];

        return new ReactionSummaryResponse
        {
            TotalCount = summary?.ReactionCount ?? 0,
            Reactions = counts
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new ReactionCountResponse
                {
                    ReactionKey = entry.Key,
                    Count = entry.Value,
                    ReactedByViewer = viewer.Contains(entry.Key, StringComparer.Ordinal)
                })
                .ToList(),
            ViewerReactions = viewer.ToList(),
            LikeCount = counts.GetValueOrDefault(Like),
            HelpfulCount = counts.GetValueOrDefault(Helpful),
            ConfirmationCount = counts.GetValueOrDefault(Confirmed),
            DisputeCount = counts.GetValueOrDefault(Disputed)
        };
    }

    public static ReactionSummaryResponse Empty { get; } = new()
    {
        TotalCount = 0,
        Reactions = [],
        ViewerReactions = [],
        LikeCount = 0,
        HelpfulCount = 0,
        ConfirmationCount = 0,
        DisputeCount = 0
    };
}
