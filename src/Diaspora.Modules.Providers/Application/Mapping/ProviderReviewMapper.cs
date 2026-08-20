using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Modules.Providers.Domain.ValueObjects;

namespace Diaspora.Modules.Providers.Application.Mapping;

/// <summary>Turns reviews and their rollup into the shapes the wire reads.</summary>
public static class ProviderReviewMapper
{
    public static ProviderReviewResponse ToResponse(ProviderReview review, string? reviewerName = null)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new ProviderReviewResponse
        {
            ReviewId = review.Id.Value,
            ProviderId = review.ProviderId.Value,
            ReviewerId = review.ReviewerId.Value,
            ReviewerName = reviewerName,
            Rating = review.Rating,
            Title = review.Title,
            Body = review.Body,
            Status = ProviderWireNames.ToWire(review.Status),
            ServiceId = review.ServiceId?.Value,
            Response = review.Response,
            RespondedAt = review.RespondedAt,
            IsVerifiedInteraction = review.IsVerifiedInteraction,
            HelpfulCount = review.HelpfulCount,
            IsEdited = review.IsEdited,
            CreatedAt = review.CreatedAt,
            EditedAt = review.EditedAt
        };
    }

    /// <summary>
    /// The rollup, with the distribution keyed by star-as-string.
    /// <para>
    /// String keys because that is how the client declares it — its
    /// <c>Record&lt;'1'|'2'|'3'|'4'|'5', number&gt;</c> — and because JSON object keys are
    /// strings anyway. Emitting integer keys would round-trip through the same wire and land as
    /// strings on the client with no benefit and one more place to disagree.
    /// </para>
    /// </summary>
    public static ProviderReviewSummaryResponse ToResponse(ProviderReviewSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProviderReviewSummaryResponse
        {
            Average = summary.Average,
            TotalReviews = summary.TotalReviews,
            Distribution = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["1"] = summary.OneStar,
                ["2"] = summary.TwoStar,
                ["3"] = summary.ThreeStar,
                ["4"] = summary.FourStar,
                ["5"] = summary.FiveStar
            }
        };
    }
}
