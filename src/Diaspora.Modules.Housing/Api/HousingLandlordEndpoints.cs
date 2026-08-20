using Diaspora.Modules.Housing.Application.Landlords;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Housing.Api;

/// <summary>
/// Landlord profiles.
/// <para>
/// The read is anonymous: a member deciding whether to trust a letting agency should be able to look
/// it up before they join anything, and the whole point of a verified badge is that somebody outside
/// the platform can see it.
/// </para>
/// </summary>
internal static class HousingLandlordEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/landlords", async (
                [FromBody] RegisterLandlordRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new RegisterLandlordCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(HousingPolicies.LandlordCreate)
            .WithName("RegisterLandlord")
            .WithSummary("Set up a landlord profile. One per member.");

        group.MapPatch("/landlords/me", async (
                [FromBody] UpdateLandlordRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateLandlordCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(HousingPolicies.LandlordManage)
            .WithName("UpdateLandlord")
            .WithSummary("Revise the caller landlord profile.");

        group.MapGet("/landlords/{idOrSlug}", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetLandlordQuery { IdOrSlug = idOrSlug }, cancellationToken)))
            .AllowAnonymous()
            .WithName("GetLandlord")
            .WithSummary("One landlord profile, redacted for the caller.");

        group.MapPost("/landlords/me/verification", async (
                [FromBody] SubmitHousingVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitLandlordVerificationCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.LandlordSubmitVerification)
            .WithName("SubmitLandlordVerification")
            .WithSummary("Ask Trust to verify the caller landlord profile.");
    }
}
