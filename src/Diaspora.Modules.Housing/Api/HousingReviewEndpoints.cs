using Diaspora.Modules.Housing.Application.Reviews;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Housing.Api;

/// <summary>
/// Landlord ratings.
/// <para>
/// The read is anonymous and the write is not, which is the asymmetry the feature needs: a
/// rating exists to inform somebody deciding whether to trust a landlord with a deposit — often
/// before they have an account — while writing one requires having lived there.
/// </para>
/// </summary>
internal static class HousingReviewEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/landlords/{landlordId:guid}/reviews", async (
                Guid landlordId,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListLandlordReviewsQuery
                        {
                            LandlordId = landlordId,
                            Cursor = cursor,
                            Limit = limit ?? HousingLimits.DefaultPageSize
                        },
                        cancellationToken)))
            .AllowAnonymous()
            .WithName("ListLandlordReviews")
            .WithSummary("A landlord's reviews, with the rating rollup beside them.");

        group.MapPost("/landlords/{landlordId:guid}/reviews", async (
                Guid landlordId,
                [FromBody] WriteLandlordReviewRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new WriteLandlordReviewCommand
                        {
                            LandlordId = landlordId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ReviewWrite)
            .WithName("WriteLandlordReview")
            .WithSummary("Rate a tenancy you moved into. Refused without one.");
    }
}
