using Diaspora.Modules.Guides.Application.Knowledge.Curation;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Editorial curation: what a country leads with, and what to read next.
/// <para>
/// Gated at <see cref="KnowledgePolicies.Curate"/>, which moderators hold as well as administrators.
/// A moderator already decides what stays up; deciding what goes first is the same kind of
/// judgement, and the alternative — routing every featuring decision through an administrator —
/// makes curation something nobody has time to do.
/// </para>
/// <para>
/// <b>Featuring is country-scoped and does not touch the guide.</b> B8 owns the guide; this places
/// an identifier on a country's shelf. Unfeaturing takes the category key as a query parameter
/// rather than a path segment, because the country shelf has no key and a route with an optional
/// segment is a route with two shapes.
/// </para>
/// </summary>
internal static class KnowledgeCurationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/admin/countries/{countryCode}/featured", async (
                string countryCode,
                FeatureKnowledgeGuideRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new FeatureKnowledgeGuideCommand
                        {
                            CountryCode = countryCode,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.Curate)
            .WithName("FeatureKnowledgeGuide")
            .WithSummary("Put a guide at the top for this country, or within one of its rows.");

        group.MapDelete("/admin/countries/{countryCode}/featured/{guideId:guid}", async (
                string countryCode,
                Guid guideId,
                string? categoryKey,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UnfeatureKnowledgeGuideCommand
                        {
                            CountryCode = countryCode,
                            GuideId = guideId,
                            KnowledgeCategoryKey = categoryKey
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.Curate)
            .WithName("UnfeatureKnowledgeGuide")
            .WithSummary("Take a guide off a shelf. It stays published and discoverable.");

        group.MapPut("/admin/guides/{guideId:guid}/next", async (
                Guid guideId,
                SetRecommendedReadsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetRecommendedReadsCommand { GuideId = guideId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.Curate)
            .WithName("SetKnowledgeRecommendedReads")
            .WithSummary("Set what to read after this guide. Curated, never inferred.");
    }
}
