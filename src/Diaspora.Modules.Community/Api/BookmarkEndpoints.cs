using Diaspora.Modules.Community.Application.Bookmarks;
using Diaspora.Modules.Community.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>
/// Bookmarks and their collections.
/// <para>
/// Under the module's own prefix, not at the top level: <c>/api/v1/bookmarks</c> belongs to the
/// B2.6 reference module and stays there. The two are different capabilities.
/// </para>
/// </summary>
internal static class BookmarkEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/bookmarks", async (
                CreateBookmarkRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new SaveBookmarkCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Bookmark)
            .WithName("SaveCommunityBookmark")
            .WithSummary("Save anything the platform can address.");

        group.MapDelete("/bookmarks/{contentType}/{contentId:guid}", async (
                string contentType,
                Guid contentId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RemoveBookmarkCommand
                        {
                            ContentType = contentType, ContentId = contentId
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Bookmark)
            .WithName("RemoveCommunityBookmark")
            .WithSummary("Unsave something.");

        group.MapGet("/bookmarks/collections", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new ListBookmarkCollectionsQuery(), cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Bookmark)
            .WithName("ListCommunityBookmarkCollections")
            .WithSummary("The saved screen: type tiles and named folders together.");

        group.MapPost("/bookmarks/folders", async (
                CreateBookmarkFolderRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateBookmarkFolderCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Bookmark)
            .WithName("CreateCommunityBookmarkFolder")
            .WithSummary("Create a folder that spans content types.");
    }
}
