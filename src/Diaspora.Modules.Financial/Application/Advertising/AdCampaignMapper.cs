using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Advertising;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Application.Advertising;

/// <summary>
/// The one projection from an advertising campaign to the wire.
/// <para>
/// <b>The five "can I" flags are decided here and nowhere else.</b> Every one of them is a question
/// about a twelve-state machine, and a client that derived "pausable" from a status string would be
/// re-implementing that machine on a device, in a language that cannot see the aggregate — which is
/// how a Pause button appears on a draft.
/// </para>
/// <para>
/// <b>The reviewer's identity does not cross the wire.</b> The decision, the note and the date do.
/// Who made it is not something an advertiser needs, and is something a rejected advertiser might
/// act on.
/// </para>
/// </summary>
internal static class AdCampaignMapper
{
    public static AdCampaignResponse ToResponse(AdCampaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return new AdCampaignResponse
        {
            Id = campaign.Id.Value,
            Name = campaign.Name,
            Objective = campaign.Objective.ToString(),
            Status = campaign.Status.ToString(),

            Budget = ToResponse(campaign.Budget),
            Spend = ToResponse(campaign.Spend),
            Remaining = ToResponse(campaign.RemainingBudget),
            Settled = campaign.SettledAmount is { } settled ? ToResponse(settled) : null,

            StartsAt = campaign.Schedule.StartsAt,
            EndsAt = campaign.Schedule.EndsAt,

            Audience = ToResponse(campaign.Audience),

            CreativeMediaId = campaign.CreativeMediaId,
            Headline = campaign.Headline,
            CallToActionLabel = campaign.CallToActionLabel,
            Body = campaign.Body,

            SubjectType = campaign.Subject?.SubjectType.ToString(),
            SubjectId = campaign.Subject?.SubjectId,
            ProductCode = campaign.ProductCode,
            PurchaseId = campaign.PurchaseId?.Value,

            Placements = [.. campaign.Placements.Select(placement => new AdCampaignPlacementResponse
            {
                PlacementId = placement.PlacementId.Value,
                Kind = placement.Kind.ToString()
            })],

            Review = ToReviewResponse(campaign),

            SubmittedAt = campaign.SubmittedAt,
            ActivatedAt = campaign.ActivatedAt,
            PausedAt = campaign.PausedAt,
            EndedAt = campaign.EndedAt,
            EndReason = campaign.EndReason,
            CreatedAt = campaign.CreatedAt,

            // The state machine's answers, given rather than implied.
            CanEdit = campaign.IsEditable,
            CanSubmit = campaign.Status == AdCampaignStatus.Draft,
            CanPause = campaign.Status == AdCampaignStatus.Active,
            CanResume = campaign.Status == AdCampaignStatus.Paused,
            CanCancel = !campaign.IsTerminal
        };
    }

    public static AdCampaignReviewResponse ToReviewResponse(AdCampaign campaign)
    {
        ArgumentNullException.ThrowIfNull(campaign);

        return new AdCampaignReviewResponse
        {
            Decision = campaign.ReviewDecision.ToString(),

            // The note travels on an approval too. A reviewer who wrote "approved, but keep the
            // claim about delivery times out of the next one" wrote it to be read.
            Note = campaign.ReviewNote,
            DecidedAt = campaign.ReviewedAt,

            CanAppeal = campaign.Status == AdCampaignStatus.Rejected && campaign.AppealedAt is null,
            AppealNote = campaign.AppealNote,
            AppealedAt = campaign.AppealedAt
        };
    }

    public static AdAudienceResponse ToResponse(AdAudience audience)
    {
        ArgumentNullException.ThrowIfNull(audience);

        return new AdAudienceResponse
        {
            CountryCodes = [.. audience.CountryCodes],
            CommunityIds = [.. audience.CommunityIds],
            NeighbourhoodIds = [.. audience.NeighbourhoodIds],
            Interests = [.. audience.Interests],
            Categories = [.. audience.Categories],
            TownNames = [.. audience.TownNames],
            IsEveryone = audience.IsEveryone,
            ConstrainedDimensions =
                [.. audience.ConstrainedDimensions().Select(dimension => dimension.ToString())]
        };
    }

    public static AdMoneyResponse ToResponse(Money money)
    {
        ArgumentNullException.ThrowIfNull(money);

        return new AdMoneyResponse
        {
            MinorUnits = money.MinorUnits,
            Currency = money.Currency,
            Exponent = money.Exponent
        };
    }
}
