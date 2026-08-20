using Diaspora.Modules.Location.Application.Experience;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// Residence history: the countries a member has lived in.
/// <para>
/// Location serves the claim. Submitting evidence for it is <c>POST
/// /api/v1/trust/verifications/country</c> and stays Trust's — nothing here creates,
/// evaluates or revokes a verification.
/// </para>
/// </summary>
internal static class ExperienceEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/me/countries", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyCountryExperienceQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadExperience)
            .WithName("GetMyCountryExperience")
            .WithSummary("The caller's residence timeline, with Trust's verification state.");

        group.MapPost("/me/countries", async (
                DeclareCountryExperienceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeclareCountryExperienceCommand { Experience = request }, cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageExperience)
            .WithName("DeclareCountryExperience")
            .WithSummary("Declare — or amend — where you have lived.");

        // PUT and POST reach the same command. The frontend's wizard posts on first save and
        // puts on edit, and an upsert makes both correct without the client having to know
        // which it is.
        group.MapPut("/me/countries/{countryCode}", async (
                string countryCode,
                DeclareCountryExperienceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new DeclareCountryExperienceCommand
                    {
                        Experience = request with { CountryCode = countryCode }
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageExperience)
            .WithName("UpdateCountryExperience")
            .WithSummary("Amend a declared residence.");

        group.MapDelete("/me/countries/{countryCode}", async (
                string countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RemoveCountryExperienceCommand { CountryCode = countryCode },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageExperience)
            .WithName("RemoveCountryExperience")
            .WithSummary("Withdraw a declared residence. Does not touch its verification.");

        group.MapGet("/users/{userId:guid}/countries", async (
                Guid userId, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetPublicCountryExperienceQuery { UserId = userId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadPublic)
            .WithName("GetPublicCountryExperience")
            .WithSummary("Another member's public country experience.");
    }
}
