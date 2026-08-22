using Diaspora.Modules.Providers.Application.Bookmarks;
using Diaspora.Modules.Providers.Application.Categories;
using Diaspora.Modules.Providers.Application.Reviews;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Providers.Api;

/// <summary>
/// Reviews, categories and bookmarks.
/// <para>
/// The bookmark routes are a <c>PUT</c> and a <c>DELETE</c> on the same resource rather than two
/// verbs on a collection, so both are idempotent: a client that retries a save after a timeout
/// gets the same answer, and a double-tapped heart cannot produce two bookmarks. The storage is
/// Community's — see <c>ProviderBookmarkService</c> and deviation D-2.
/// </para>
/// </summary>
internal static class ProviderReviewEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapCategories(group);
        MapReviews(group);
        MapBookmarks(group);
    }

    private static void MapCategories(RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (
                Guid? parentId,
                bool? includeRetired,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListServiceCategoriesQuery
                        {
                            ParentId = parentId,
                            IncludeRetired = includeRetired ?? false
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("ListServiceCategories")
            .WithSummary("The service category catalogue, ordered depth-first.");
    }

    private static void MapReviews(RouteGroupBuilder group)
    {
        // ── The caller's own reviews ────────────────────────────────────────────
        //
        // Declared before `/{providerId:guid}/reviews` so the literal `me` resolves here. The
        // client asked for `GET /providers/me/reviews`, got a bare 404, and the creator profile
        // has carried "Diaspora cannot list your reviews yet" ever since — while
        // `/{providerId}/reviews` worked perfectly and nothing could tell a member which provider
        // id was theirs.
        //
        // A member who runs no provider gets an **empty page**, not a 404: "you have no reviews"
        // and "you are not a provider" are different sentences, and the second one is not an
        // error. The screen can tell them apart from `providerId` being null.
        group.MapGet("/me/reviews", async (
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMyProviderReviewsQuery { Cursor = cursor, Limit = limit },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("ListMyProviderReviews")
            .WithSummary("Reviews left on the providers the caller runs.");

        group.MapGet("/{providerId:guid}/reviews", async (
                Guid providerId,
                string? cursor,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListProviderReviewsQuery
                        {
                            ProviderId = providerId,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("ListProviderReviews")
            .WithSummary("A page of reviews, newest first. The rollup rides on page one.");

        group.MapPost("/{providerId:guid}/reviews", async (
                Guid providerId,
                WriteProviderReviewRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new WriteProviderReviewCommand
                        {
                            ProviderId = providerId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Review)
            .WithName("WriteProviderReview")
            .WithSummary("Review a provider. Writing twice revises your existing review.");
    }

    private static void MapBookmarks(RouteGroupBuilder group)
    {
        group.MapPut("/{providerId:guid}/bookmark", async (
                Guid providerId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetProviderBookmarkCommand { ProviderId = providerId, IsSaved = true },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Bookmark)
            .WithName("SaveProvider")
            .WithSummary("Save a provider. Idempotent.");

        group.MapDelete("/{providerId:guid}/bookmark", async (
                Guid providerId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetProviderBookmarkCommand { ProviderId = providerId, IsSaved = false },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Bookmark)
            .WithName("UnsaveProvider")
            .WithSummary("Remove a saved provider. Idempotent.");
    }
}
