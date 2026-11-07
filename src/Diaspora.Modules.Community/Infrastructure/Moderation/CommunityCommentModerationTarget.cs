using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Infrastructure.Moderation;

/// <summary>
/// A comment, on anything Community carries comments for, as a Trust moderation case sees it
/// (7 Nov 2026).
/// <para>
/// A hide pending review takes the comment out of its thread; a confirmed removal tombstones it,
/// so the thread keeps its shape and says a message was removed. Neither is the author's deletion,
/// so what the comment earned is not taken back by the decision itself. A case found to be fine
/// takes off the automatic "reported" flag. The owner is the comment's author.
/// </para>
/// </summary>
public sealed class CommunityCommentModerationTarget : IModerationTarget
{
    private const int TitleLength = 120;

    private readonly ICommentRepository _comments;

    public CommunityCommentModerationTarget(ICommentRepository comments) => _comments = comments;

    public string ContentType => ModerationContentTypes.CommunityComment;

    public string Area => ModerationAreas.Community;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var comment = await _comments.GetAsync(CommentId.From(contentId), cancellationToken);

        return comment is null
            ? null
            : new ModerationTargetSnapshot(
                comment.Id.Value,
                comment.AuthorId.Value,
                Excerpt(comment.Body.Value),
                CommunityWireNames.ToWire(comment.Status));
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var comment = await _comments.GetAsync(CommentId.From(contentId), cancellationToken);

        if (comment is null)
        {
            return Result.Failure<ModerationTargetChange>(Error.NotFound(
                "community.comment.not-found", "The comment this case is about no longer exists."));
        }

        var before = CommunityWireNames.ToWire(comment.Status);

        var applied = outcome switch
        {
            ModerationOutcome.HidePendingReview => comment.HidePendingReview(),
            ModerationOutcome.Remove => comment.RemoveByModeration(),
            ModerationOutcome.Restore => comment.RestoreFromModeration(),
            ModerationOutcome.NoAction => comment.ClearReportedFlag(),
            _ => Result.Failure(Error.Validation(
                "community.moderation.outcome", $"'{outcome}' is not a decision."))
        };

        return applied.IsFailure
            ? Result.Failure<ModerationTargetChange>(applied.Error)
            : Result.Success(new ModerationTargetChange(before, CommunityWireNames.ToWire(comment.Status)));
    }

    private static string Excerpt(string body)
    {
        var trimmed = body.Trim();

        return trimmed.Length > TitleLength ? trimmed[..TitleLength] + "…" : trimmed;
    }
}
