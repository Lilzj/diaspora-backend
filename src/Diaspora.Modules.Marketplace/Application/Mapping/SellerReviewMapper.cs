using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Reviews;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;

namespace Diaspora.Modules.Marketplace.Application.Mapping;

/// <summary>Wire shapes for seller reviews and the rollup beside them.</summary>
public static class SellerReviewMapper
{
    public static SellerReviewResponse ToResponse(this SellerReview review, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(review);

        return new SellerReviewResponse
        {
            ReviewId = review.Id.Value,
            SellerId = review.SellerId.Value,
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
                && now - review.CreatedAt <= SellerReview.EditWindow,

            EditedAt = review.EditedAt,
            CreatedAt = review.CreatedAt
        };
    }

    public static SellerRatingResponse ToResponse(this SellerReviewSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new SellerRatingResponse
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
