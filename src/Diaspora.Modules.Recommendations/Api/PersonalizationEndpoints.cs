using Diaspora.Modules.Recommendations.Application.Explainability;
using Diaspora.Modules.Recommendations.Application.Preferences;
using Diaspora.Modules.Recommendations.Application.Profiles;
using Diaspora.Modules.Recommendations.Application.Search;
using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Recommendations.Api;

/// <summary>
/// The caller's own profile, preferences and controls.
/// <para>
/// <b>The transparency surface, and it is deliberately as prominent as the feed.</b> A member can
/// read exactly what the platform believes about them, change what it uses, block what they never
/// want to see, and turn the whole thing off — from routes that are as easy to find as the ones that
/// serve the recommendations.
/// </para>
/// <para>
/// None of these routes takes a user identifier. See <see cref="FeedEndpoints"/>.
/// </para>
/// </summary>
internal static class PersonalizationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/profile", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetPersonalizationProfileQuery(), cancellationToken)))
            .WithName("GetPersonalizationProfile")
            .WithSummary("Everything the platform believes about the caller.");

        group.MapPost("/profile/refresh", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new RefreshProfileCommand(), cancellationToken)))
            .WithName("RefreshPersonalizationProfile")
            .WithSummary("Folds the caller's pending observations in now.");

        group.MapPost("/profile/rebuild", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new RebuildProfileCommand(), cancellationToken)))
            .WithName("RebuildPersonalizationProfile")
            .WithSummary("Recomputes the caller's profile from scratch. Rate-limited.");

        group.MapGet("/preferences", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetPersonalizationPreferencesQuery(), cancellationToken)))
            .WithName("GetPersonalizationPreferences")
            .WithSummary("What the caller has asked for, or the defaults.");

        group.MapPut("/preferences", async (
                UpdatePersonalizationPreferencesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdatePersonalizationPreferencesCommand
                        {
                            IsPersonalizationEnabled = request.IsPersonalizationEnabled,
                            ShareActivitySignals = request.ShareActivitySignals,
                            PreferredLanguage = request.PreferredLanguage,
                            PreferredCountryCode = request.PreferredCountryCode,
                            Categories = request.Categories,
                            Surfaces = request.Surfaces,
                            FavoriteLocations = request.FavoriteLocations
                        },
                        cancellationToken)))
            .WithName("UpdatePersonalizationPreferences")
            .WithSummary("Changes what the caller has asked for. Omitted fields are left alone.");

        group.MapPost("/preferences/blocks", async (
                BlockRecommendationTargetRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new BlockRecommendationTargetCommand
                        {
                            TargetType = request.TargetType,
                            TargetId = request.TargetId
                        },
                        cancellationToken)))
            .WithName("BlockRecommendationTarget")
            .WithSummary("Never recommend this to the caller again. Permanent, unlike a dismissal.");

        group.MapDelete("/preferences/blocks/{targetType}/{targetId:guid}", async (
                string targetType,
                Guid targetId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new BlockRecommendationTargetCommand
                        {
                            TargetType = targetType,
                            TargetId = targetId,
                            Remove = true
                        },
                        cancellationToken)))
            .WithName("UnblockRecommendationTarget")
            .WithSummary("Lifts a block.");

        group.MapGet("/reasons", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetRecommendationReasonsQuery(), cancellationToken)))
            .WithName("GetRecommendationReasons")
            .WithSummary("Every reason the platform can give, for a client's translation bundle.");

        group.MapGet("/search-signals", async (
                [FromQuery] bool? trending,
                [FromQuery] string? country,
                [FromQuery] string? type,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetSearchSignalsQuery
                        {
                            Trending = trending ?? false,
                            CountryCode = country,
                            TargetType = type
                        },
                        cancellationToken)))
            .WithName("GetSearchSignals")
            .WithSummary("Personalization boosts for a search engine. Never results.");
    }
}
