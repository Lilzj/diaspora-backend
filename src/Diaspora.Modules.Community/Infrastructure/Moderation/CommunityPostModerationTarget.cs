using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Infrastructure.Moderation;

/// <summary>
/// A community post, as a Trust moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff act on a member's post now.</b> Community's own staff decision route is
/// gone; a decision is made on the Trust case and carried out here, in the same transaction.
/// </para>
/// <para>
/// A hide pending review uses the post's Hidden state, which its author can still edit and ask to
/// have looked at again. A confirmed removal uses Removed, which nobody can edit and only a restore
/// lifts. Neither deletes the post, so what it earned is not taken back by the decision itself.
/// The owner is the post's author: the member who wrote it, also when it was published as an
/// organisation.
/// </para>
/// </summary>
public sealed class CommunityPostModerationTarget : IModerationTarget
{
    private const int TitleLength = 140;

    private readonly ICommunityPostRepository _posts;

    public CommunityPostModerationTarget(ICommunityPostRepository posts) => _posts = posts;

    public string ContentType => ModerationContentTypes.CommunityPost;

    public string Area => ModerationAreas.Community;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var post = await _posts.GetForReadAsync(CommunityPostId.From(contentId), cancellationToken);

        return post is null
            ? null
            : new ModerationTargetSnapshot(
                post.Id.Value,
                post.AuthorId.Value,
                Excerpt(post.Title ?? post.Body.Value),
                CommunityWireNames.ToWire(post.Status));
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var post = await _posts.GetAsync(CommunityPostId.From(contentId), cancellationToken);

        if (post is null)
        {
            return Result.Failure<ModerationTargetChange>(Error.NotFound(
                "community.post.not-found", "The post this case is about no longer exists."));
        }

        var before = CommunityWireNames.ToWire(post.Status);

        var applied = outcome switch
        {
            ModerationOutcome.HidePendingReview => post.HidePendingReview(),
            ModerationOutcome.Remove => post.RemoveByModeration(),
            ModerationOutcome.Restore => post.RestoreFromModeration(),

            // A post carries no automatic flag (its report count is a counter, not a state), so a
            // case found to be fine changes nothing on it.
            ModerationOutcome.NoAction => Result.Success(),
            _ => Result.Failure(Error.Validation(
                "community.moderation.outcome", $"'{outcome}' is not a decision."))
        };

        return applied.IsFailure
            ? Result.Failure<ModerationTargetChange>(applied.Error)
            : Result.Success(new ModerationTargetChange(before, CommunityWireNames.ToWire(post.Status)));
    }

    private static string Excerpt(string text)
    {
        var trimmed = text.Trim();

        return trimmed.Length > TitleLength ? trimmed[..TitleLength] + "…" : trimmed;
    }
}
