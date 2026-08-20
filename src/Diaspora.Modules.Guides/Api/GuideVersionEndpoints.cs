using Diaspora.Modules.Guides.Application.Versioning;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// A guide's publication history, and the one write that reaches back into it.
/// <para>
/// <b>The reads are public, because the history is the accountability.</b> A reader who acted on
/// a fee, a deadline or a document list should be able to see that it changed and when — and a
/// version history visible only to its author is a record kept by the one person it could
/// exonerate.
/// </para>
/// <para>
/// Versions are addressed by <em>number</em> rather than by row identifier: the number is what
/// the history list renders, what a citation quotes and what an author asks to restore.
/// Restoring writes the draft and nothing else — the versions themselves are immutable, so a
/// guide that once gave wrong advice cannot rewrite the record of having given it.
/// </para>
/// </summary>
internal static class GuideVersionEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapHistory(group);
        MapRestore(group);
    }

    private static void MapHistory(RouteGroupBuilder group)
    {
        group.MapGet("/{guideId:guid}/versions", async (
                Guid guideId,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListGuideVersionsQuery { GuideId = guideId, Limit = limit },
                        cancellationToken)))
            .WithName("ListGuideVersions")
            .WithSummary("The publication history, newest first. Summaries only.");

        group.MapGet("/{guideId:guid}/versions/compare", async (
                Guid guideId,
                int fromVersion,
                int toVersion,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CompareGuideVersionsQuery
                        {
                            GuideId = guideId,
                            FromVersion = fromVersion,
                            ToVersion = toVersion
                        },
                        cancellationToken)))
            .WithName("CompareGuideVersions")
            .WithSummary("What changed between two versions, at section granularity.");

        group.MapGet("/{guideId:guid}/versions/{versionNumber:int}", async (
                Guid guideId,
                int versionNumber,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetGuideVersionQuery
                        {
                            GuideId = guideId,
                            VersionNumber = versionNumber
                        },
                        cancellationToken)))
            .WithName("GetGuideVersion")
            .WithSummary("One published version in full — what readers were actually shown.");
    }

    private static void MapRestore(RouteGroupBuilder group)
    {
        group.MapPost("/{guideId:guid}/versions/{versionNumber:int}/restore", async (
                Guid guideId,
                int versionNumber,
                RestoreGuideVersionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreGuideVersionCommand
                        {
                            GuideId = guideId,
                            VersionNumber = versionNumber,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(GuidePolicies.EditOwn)
            .WithName("RestoreGuideVersion")
            .WithSummary("Apply an old version's content back onto the draft. Publishing mints the next version.");
    }
}
