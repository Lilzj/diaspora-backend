using Diaspora.Modules.Marketplace.Application.Reviews;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Marketplace.Api;

/// <summary>
/// Seller ratings.
/// <para>
/// The read is anonymous and the write is not, which is the asymmetry the feature needs: a
/// rating exists to inform somebody deciding whether to trust a seller — often before they have
/// an account — while writing one requires having completed a purchase.
/// </para>
/// </summary>
internal static class MarketplaceReviewEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/sellers/{sellerId:guid}/reviews", async (
                Guid sellerId,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListSellerReviewsQuery
                        {
                            SellerId = sellerId,
                            Cursor = cursor,
                            Limit = limit ?? MarketplaceLimits.DefaultPageSize
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListSellerReviews")
            .WithSummary("A seller's reviews, with the rating rollup beside them.");

        group.MapPost("/sellers/{sellerId:guid}/reviews", async (
                Guid sellerId,
                [FromBody] WriteSellerReviewRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new WriteSellerReviewCommand { SellerId = sellerId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(MarketplacePolicies.ReviewWrite)
            .WithName("WriteSellerReview")
            .WithSummary("Rate a completed purchase. Refused without one.");
    }
}
