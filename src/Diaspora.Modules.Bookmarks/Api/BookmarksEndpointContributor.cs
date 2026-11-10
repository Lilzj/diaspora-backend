using Diaspora.Modules.Bookmarks.Application;
using Diaspora.Modules.Bookmarks.Contracts;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Bookmarks.Api;

/// <summary>
/// Maps the module's HTTP surface as minimal APIs.
/// <para>
/// Every route dispatches through <c>ISender</c> — so validation, authorisation, telemetry
/// and transactions run in the shared pipeline — and returns through <see cref="PlatformResults"/>,
/// so the envelope and problem documents are identical to the host's controllers. The
/// endpoints reference the module's own policy names; the kernel has already registered
/// those with ASP.NET Core from the policy contributor. Nothing here is known to the host.
/// </para>
/// </summary>
public sealed class BookmarksEndpointContributor : IEndpointContributor
{
    public string Module => BookmarksModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(BookmarksModuleMetadata.RoutePrefix)
            .WithTags("Bookmarks")
            // The comment this line used to carry — "reuse the authenticated default bucket" —
            // named a bucket that did not exist. "diaspora-signin" is credential exchange, and
            // bookmarks were being refused on their first request because a conversation list
            // had already spent the shared per-IP budget. This one does exist.
            .RequireRateLimiting(RateLimitPolicyNames.Authenticated);

        group.MapGet("/", async (ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ListBookmarksQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(BookmarkPolicies.Read)
            .WithName("ListBookmarks");

        group.MapPost("/", async (
                CreateBookmarkRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new CreateBookmarkCommand
                {
                    TargetType = request.TargetType,
                    TargetId = request.TargetId,
                    Note = request.Note
                }, cancellationToken);

                return PlatformResults.Created(context, result);
            })
            .RequireAuthorization(BookmarkPolicies.Manage)
            .WithName("CreateBookmark");

        group.MapDelete("/{bookmarkId:guid}", async (
                Guid bookmarkId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RemoveBookmarkCommand { BookmarkId = bookmarkId }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(BookmarkPolicies.Manage)
            .WithName("RemoveBookmark");
    }
}
