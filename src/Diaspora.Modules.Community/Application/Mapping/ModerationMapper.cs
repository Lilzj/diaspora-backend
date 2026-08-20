using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Moderation;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>Projects reports, decisions and automated reviews.</summary>
public static class ModerationMapper
{
    public static ReportResponse ToResponse(ContentReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new ReportResponse
        {
            ReportId = report.Id.Value,
            ReporterId = report.ReporterId.Value,
            ReporterName = null,
            ContentType = CommunityWireNames.ToWire(report.Content.Type),
            ContentId = report.Content.Id,
            ReasonCode = CommunityWireNames.ToWire(report.Reason),
            Description = report.Description,
            EvidenceUrl = report.EvidenceUrl,
            Status = CommunityWireNames.ToWire(report.Status),
            ResolvedBy = report.ResolvedBy,
            ResolutionNote = report.ResolutionNote,
            CreatedAt = report.ReportedAt,
            ResolvedAt = report.ResolvedAt
        };
    }

    public static ModerationDecisionResponse ToResponse(ModerationDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new ModerationDecisionResponse
        {
            ActionId = decision.Id.Value,
            ModeratorId = decision.ModeratorId,
            ModeratorName = null,
            ContentType = CommunityWireNames.ToWire(decision.Content.Type),
            ContentId = decision.Content.Id,
            ActionType = CommunityWireNames.ToWire(decision.Action),
            Source = CommunityWireNames.ToWire(decision.Source),
            Reason = decision.Reason,
            PreviousStatus = decision.PreviousStatus,
            NewStatus = decision.NewStatus,
            CreatedAt = decision.DecidedAt
        };
    }

    public static ContentReviewResponse ToResponse(ContentReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new ContentReviewResponse
        {
            ReviewId = review.Id.Value,
            ContentType = CommunityWireNames.ToWire(review.Content.Type),
            ContentId = review.Content.Id,
            Status = CommunityWireNames.ToWire(review.Status),
            Flags = review.ParsedFlags.Select(CommunityWireNames.ToWire).ToList(),
            Confidence = review.Confidence,
            Provider = review.Provider,
            ProviderVersion = review.ProviderVersion,
            ProcessedAt = review.ReviewedAt
        };
    }

    /// <summary>The reason catalogue a client builds its report modal from.</summary>
    public static IReadOnlyList<ReportReasonResponse> Reasons() =>
        Enum.GetValues<ReportReasonCode>()
            .Select(reason => new ReportReasonResponse
            {
                Code = CommunityWireNames.ToWire(reason),
                Label = CommunityWireNames.ReasonLabel(reason),

                // Only misinformation is unactionable without a link — it is the client's
                // conflicting-information flow, whose whole claim is that one post contradicts
                // another.
                RequiresEvidence = reason == ReportReasonCode.Misinformation
            })
            .ToList();
}
