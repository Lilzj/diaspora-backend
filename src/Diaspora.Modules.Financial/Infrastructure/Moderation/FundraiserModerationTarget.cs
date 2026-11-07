using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Infrastructure.Moderation;

/// <summary>
/// A fundraising campaign, as a moderation case sees it (7 Nov 2026).
/// <para>
/// <b>The only way staff take a member's fundraiser down now.</b> The organiser's routes refuse
/// staff, and the stage-2 suspend route is gone: a decision is made on the Trust case and carried
/// out here, in the same transaction, with the moderator's note as the reason the organiser reads.
/// </para>
/// <para>
/// A hide pending review and a removal both use <see cref="CampaignStatus.Suspended"/>: the one
/// state the organiser can neither enter nor leave, so a removal is permanent unless an appeal
/// succeeds, and the case knows which of the two it is. Neither closes, cancels or completes the
/// campaign, and nothing is refunded; the cause closes with the suspension so the money stops, as
/// it always has. A restore brings the campaign back paused, for its organiser to resume.
/// </para>
/// <para>
/// Fundraiser review (publishing or rejecting a submitted campaign) is not moderation and does not
/// come through here.
/// </para>
/// </summary>
public sealed class FundraiserModerationTarget : IModerationTarget
{
    private readonly ICampaignRepository _campaigns;
    private readonly IDonationCauseRepository _causes;
    private readonly IClock _clock;

    public FundraiserModerationTarget(
        ICampaignRepository campaigns, IDonationCauseRepository causes, IClock clock)
    {
        _campaigns = campaigns;
        _causes = causes;
        _clock = clock;
    }

    public string ContentType => ModerationContentTypes.Fundraiser;

    public string Area => ModerationAreas.Fundraisers;

    public async Task<ModerationTargetSnapshot?> DescribeAsync(
        Guid contentId, CancellationToken cancellationToken = default)
    {
        var campaign = await _campaigns.FindAsync(CampaignId.From(contentId), cancellationToken);

        // The organiser answers for the campaign and is the one told, whether or not it is run on
        // an organization's behalf.
        return campaign is null
            ? null
            : new ModerationTargetSnapshot(
                campaign.Id.Value, campaign.OrganizerUserId, campaign.Title, campaign.Status.ToString());
    }

    public async Task<Result<ModerationTargetChange>> ApplyAsync(
        Guid contentId,
        ModerationOutcome outcome,
        ModerationActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var campaign = await _campaigns.FindAsync(CampaignId.From(contentId), cancellationToken);

        if (campaign is null)
        {
            return Result.Failure<ModerationTargetChange>(Error.NotFound(
                FinancialErrorCodes.CampaignNotFound, "The fundraiser this case is about no longer exists."));
        }

        var before = campaign.Status;
        var now = _clock.UtcNow;

        var applied = outcome switch
        {
            // Already suspended (a removal confirmed after its hide): it stays as it is.
            ModerationOutcome.HidePendingReview or ModerationOutcome.Remove
                => campaign.Status == CampaignStatus.Suspended
                    ? Result.Success()
                    : campaign.Suspend(actor.Note, now),
            ModerationOutcome.Restore
                => campaign.Status == CampaignStatus.Suspended
                    ? campaign.Restore(actor.Note, now)
                    : Result.Success(),
            // A fundraiser carries no automatic flag, so a case closed with no action changes nothing.
            ModerationOutcome.NoAction => Result.Success(),
            _ => Result.Failure(Error.Validation(
                FinancialErrorCodes.CampaignModerationInvalid, $"'{outcome}' is not a decision."))
        };

        if (applied.IsFailure)
        {
            return Result.Failure<ModerationTargetChange>(applied.Error);
        }

        if (campaign.Status != before)
        {
            await KeepCauseInStepAsync(campaign, now, cancellationToken);
            _campaigns.Update(campaign);
        }

        return Result.Success(new ModerationTargetChange(before.ToString(), campaign.Status.ToString()));
    }

    /// <summary>
    /// The cause closes with the suspension, which is what makes "suspended" mean the money stops:
    /// the donation path and the recurring sweep both refuse an inactive cause. A restored campaign
    /// comes back paused, so its cause stays closed until the organiser resumes it.
    /// </summary>
    private async Task KeepCauseInStepAsync(
        Campaign campaign, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (campaign.CauseId is not { } causeId)
        {
            return;
        }

        var cause = await _causes.FindAsync(causeId, cancellationToken);

        if (cause is null || cause.IsActive == campaign.AcceptsDonations)
        {
            return;
        }

        if (campaign.AcceptsDonations)
        {
            cause.Reopen(now);
        }
        else
        {
            cause.Close(now);
        }

        _causes.Update(cause);
    }
}
