using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Modules.Community.Domain.Shares;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// The batched read every consuming module makes.
/// <para>
/// Five queries for a whole page, whatever the page contains: reaction summaries, viewer
/// reactions, comment counts, share counts, bookmark counts. A guide list of twenty items costs
/// the same as one of two.
/// </para>
/// <para>
/// A caller passing an empty set gets an empty answer without touching the database — worth
/// stating because the commonest caller is a projection loop that sometimes has nothing to ask
/// about, and five queries returning nothing is still five queries.
/// </para>
/// </summary>
public sealed class SocialSummaryService : ICommunitySocialService
{
    private readonly IReactionRepository _reactions;
    private readonly ICommentRepository _comments;
    private readonly IShareRepository _shares;
    private readonly IBookmarkRepository _bookmarks;

    public SocialSummaryService(
        IReactionRepository reactions,
        ICommentRepository comments,
        IShareRepository shares,
        IBookmarkRepository bookmarks)
    {
        _reactions = reactions;
        _comments = comments;
        _shares = shares;
        _bookmarks = bookmarks;
    }

    public async Task<IReadOnlyDictionary<string, SocialSummary>> GetSummariesAsync(
        IReadOnlyCollection<ContentReference> contents,
        UserId? viewerId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contents);

        if (contents.Count == 0)
        {
            return new Dictionary<string, SocialSummary>(StringComparer.Ordinal);
        }

        var summaries = await _reactions.GetSummariesAsync(contents, cancellationToken);
        var commentCounts = await _comments.CountManyAsync(contents, cancellationToken);
        var shareCounts = await _shares.CountManyAsync(contents, cancellationToken);
        var bookmarkCounts = await _bookmarks.CountManyAsync(contents, cancellationToken);

        var viewerReactions = viewerId is null
            ? []
            : await _reactions.ListForViewerAsync(contents, viewerId, cancellationToken);

        var saved = viewerId is null
            ? new HashSet<string>(StringComparer.Ordinal)
            : await _bookmarks.FindSavedAsync(viewerId, contents, cancellationToken);

        var byContent = viewerReactions
            .GroupBy(reaction => reaction.Content.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group
                    .Select(reaction => reaction.ReactionKey)
                    .ToList(),
                StringComparer.Ordinal);

        return contents.DistinctBy(content => content.Key).ToDictionary(
            content => content.Key,
            content =>
            {
                var summary = summaries.GetValueOrDefault(content.Key);

                return new SocialSummary
                {
                    Content = content,
                    CommentCount = commentCounts.GetValueOrDefault(content.Key),
                    ReactionCount = summary?.TotalCount ?? 0,
                    ShareCount = shareCounts.GetValueOrDefault(content.Key),
                    BookmarkCount = bookmarkCounts.GetValueOrDefault(content.Key),
                    Reactions = summary is null
                        ? new Dictionary<string, int>(StringComparer.Ordinal)
                        : summary.Tallies.ToDictionary(
                            tally => tally.ReactionKey,
                            tally => tally.Count,
                            StringComparer.Ordinal),
                    ViewerReactions = byContent.GetValueOrDefault(content.Key, []),
                    ViewerHasBookmarked = saved.Contains(content.Key)
                };
            },
            StringComparer.Ordinal);
    }
}
