using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Infrastructure.Moderation;

/// <summary>
/// A job advert, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff change a member's job now.</b> The owner's routes refuse staff, and the jobs
/// moderation routes no longer act on their own: a decision is made on the Trust case and carried
/// out here, in the same transaction, with the real moderator recorded (the Community hook this
/// replaces put the poster's own id down as the one who hid it).
/// </para>
/// <para>
/// A hide pending review and a removal both use the posting's Hidden state: nothing in this module
/// lifts it except a restore, so a removal is permanent unless an appeal succeeds. The case knows
/// which of the two it is. Neither closes or archives the job, so the employer's paid placements
/// and earned points are not touched by the decision itself.
/// </para>
/// </summary>
public sealed class JobModerationTarget : IModerationTarget
{
    private readonly IJobPostingRepository _postings;
    private readonly IEmployerProfileRepository _employers;
    private readonly IClock _clock;

    public JobModerationTarget(
        IJobPostingRepository postings, IEmployerProfileRepository employers, IClock clock)
    {
        _postings = postings;
        _employers = employers;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.Job;

    public string Area => ModerationAreas.Jobs;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var posting = await _postings.GetForReadAsync(JobPostingId.From(contentId), cancellationToken);

        if (posting is null)
        {
            return null;
        }

        var employer = await _employers.GetForReadAsync(posting.EmployerId, cancellationToken);

        // The employer's owner is who answers for the advert, and who is told.
        var owner = employer?.OwnerUserId.Value ?? posting.CreatedByUserId.Value;

        return new ModerationTargetSnapshot(
            posting.Id.Value, owner, posting.Title.Value, posting.ModerationState.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var posting = await _postings.GetAsync(JobPostingId.From(contentId), cancellationToken);

        if (posting is null)
        {
            return Result.Failure<ModerationTargetChange>(Error.NotFound(
                "jobs.posting.not-found", "The job this case is about no longer exists."));
        }

        var before = posting.ModerationState.ToString();
        var now = _clock.UtcNow;
        UserId? moderator = actor.ModeratorUserId is { } id ? UserId.From(id) : null;

        var applied = outcome switch
        {
            ModerationOutcome.HidePendingReview or ModerationOutcome.Remove
                => posting.Hide(actor.Note, moderator, now),
            ModerationOutcome.Restore => posting.Unhide(moderator, now),
            // Looked at and found to be fine: the automatic flag comes off, nothing stronger.
            ModerationOutcome.NoAction => posting.ModerationState == JobModerationState.Flagged
                ? posting.Clear(moderator, now)
                : Result.Success(),
            _ => Result.Failure(Error.Validation("jobs.moderation.outcome", $"'{outcome}' is not a decision."))
        };

        return applied.IsFailure
            ? Result.Failure<ModerationTargetChange>(applied.Error)
            : Result.Success(new ModerationTargetChange(before, posting.ModerationState.ToString()));
    }
}
