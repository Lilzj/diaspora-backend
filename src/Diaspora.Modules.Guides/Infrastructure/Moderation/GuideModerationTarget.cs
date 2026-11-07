using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag also declares a GuideId. The module-local identifier is the
// authoritative one for B8; the alias resolves the collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Infrastructure.Moderation;

/// <summary>
/// A guide, as a Trust moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff take a guide down now.</b> The guides hide and unhide routes are gone, and
/// so is the Community hook that hid a guide in the author's own name with a stock reason. A decision
/// is made on the Trust case — fed by guide reports, which Guides files through Community — and
/// carried out here, in the same transaction, with the real moderator and the moderator's note.
/// </para>
/// <para>
/// A guide has one moderation state that takes it down, Hidden, so a hide pending review and a
/// confirmed removal both use it: nothing in this module lifts it except a restore, and the case
/// knows which of the two it is. Neither archives or deletes the guide, so what it earned is not
/// taken back by the decision itself, and its author can still see and fix it. The owner is the
/// guide's author.
/// </para>
/// </summary>
public sealed class GuideModerationTarget : IModerationTarget
{
    private readonly IGuideRepository _guides;
    private readonly IClock _clock;

    public GuideModerationTarget(IGuideRepository guides, IClock clock)
    {
        _guides = guides;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.Guide;

    public string Area => ModerationAreas.Guides;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var guide = await _guides.GetForReadAsync(GuideId.From(contentId), cancellationToken);

        return guide is null
            ? null
            : new ModerationTargetSnapshot(
                guide.Id.Value, guide.AuthorId.Value, guide.Title.Value, guide.ModerationState.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var guide = await _guides.GetAsync(GuideId.From(contentId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<ModerationTargetChange>(Error.NotFound(
                "guides.guide.not-found", "The guide this case is about no longer exists."));
        }

        var before = guide.ModerationState.ToString();
        UserId? moderator = actor.ModeratorUserId is { } id ? UserId.From(id) : null;

        var applied = outcome switch
        {
            ModerationOutcome.HidePendingReview or ModerationOutcome.Remove
                => guide.Hide(actor.Note, moderator, _clock.UtcNow),
            ModerationOutcome.Restore => guide.Unhide(moderator),

            // Looked at and found to be fine: an automatic flag comes off, nothing stronger.
            ModerationOutcome.NoAction => guide.ClearFlag(),
            _ => Result.Failure(Error.Validation("guides.moderation.outcome", $"'{outcome}' is not a decision."))
        };

        return applied.IsFailure
            ? Result.Failure<ModerationTargetChange>(applied.Error)
            : Result.Success(new ModerationTargetChange(before, guide.ModerationState.ToString()));
    }
}
