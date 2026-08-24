using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Reviews;
using Diaspora.Modules.Housing.Domain.ValueObjects;

namespace Diaspora.Modules.Housing.Application.Mapping;

/// <summary>Wire shapes for landlord reviews and the rollup beside them.</summary>
public static class LandlordReviewMapper
{
    public static LandlordReviewResponse ToResponse(this LandlordReview review, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new LandlordReviewResponse
        {
            ReviewId = review.Id.Value,
            LandlordId = review.LandlordId.Value,
            ReviewerId = review.ReviewerId.Value,
            ReservationId = review.ReservationId.Value,
            ListingId = review.ListingId.Value,
            Rating = review.Rating,
            Title = review.Title,
            Body = review.Body,
            Response = review.Response,
            RespondedAt = review.RespondedAt,

            // Computed here rather than on the client, because the window is a server rule and
            // a client that worked it out locally would disagree the moment it changed.
            CanEdit = review.CountsTowardsSummary
                && now - review.CreatedAt <= LandlordReview.EditWindow,

            EditedAt = review.EditedAt,
            CreatedAt = review.CreatedAt
        };
    }

    public static LandlordRatingResponse ToResponse(this LandlordReviewSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new LandlordRatingResponse
        {
            Average = summary.Average,
            TotalReviews = summary.TotalReviews,
            Distribution = new Dictionary<string, int>
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
