using Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Curated onboarding: the sequence somebody arriving in a country is walked through.
/// <para>
/// The two reads are public — a newcomer following a link must not need an account — and the writes
/// are curator-only. A path is addressed by country and key, because the pair is what a deep link
/// carries and what lets <c>arriving</c> be the natural key for every country at once.
/// </para>
/// <para>
/// <b>The three lifecycle routes dispatch one command with a different status.</b> Publishing,
/// archiving and restoring a path are the same decision made three ways, the aggregate refuses an
/// illegal transition with the specific error, and three handlers would be triplicated plumbing with
/// no additional safety. The routes stay three, which is what a client consumes.
/// </para>
/// </summary>
internal static class KnowledgeLearningPathEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapReads(group);
        MapWrites(group);
        MapLifecycle(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group.MapGet("/countries/{countryCode}/paths", async (
                string countryCode,
                bool? includeUnpublished,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListLearningPathsQuery
                        {
                            CountryCode = countryCode,
                            IncludeUnpublished = includeUnpublished ?? false
                        },
                        cancellationToken)))
            .WithName("ListLearningPaths")
            .WithSummary("Onboarding routes for this country, without their steps.");

        group.MapGet("/countries/{countryCode}/paths/{key}", async (
                string countryCode,
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetLearningPathQuery { CountryCode = countryCode, Key = key },
                        cancellationToken)))
            .WithName("GetLearningPath")
            .WithSummary("One onboarding route, with each step's guide resolved.");
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        group.MapPost("/admin/countries/{countryCode}/paths", async (
                string countryCode,
                CreateLearningPathRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateLearningPathCommand
                        {
                            CountryCode = countryCode,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManagePaths)
            .WithName("CreateLearningPath")
            .WithSummary("Open an onboarding route as a draft.");

        group.MapPut("/admin/countries/{countryCode}/paths/{key}", async (
                string countryCode,
                string key,
                UpdateLearningPathRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateLearningPathCommand
                        {
                            CountryCode = countryCode,
                            Key = key,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManagePaths)
            .WithName("UpdateLearningPath")
            .WithSummary("Rewrite the route's own copy. Its steps are arranged separately.");

        group.MapPut("/admin/countries/{countryCode}/paths/{key}/steps", async (
                string countryCode,
                string key,
                ArrangeLearningPathRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArrangeLearningPathCommand
                        {
                            CountryCode = countryCode,
                            Key = key,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManagePaths)
            .WithName("ArrangeLearningPath")
            .WithSummary("Replace the whole sequence. Step identifiers survive an insertion.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        MapStatus(group, "publish", LearningPathStatus.Published,
            "PublishLearningPath", "Put the route in front of newcomers.");

        MapStatus(group, "archive", LearningPathStatus.Archived,
            "ArchiveLearningPath", "Withdraw it. The steps stay — they record what the advice was.");

        MapStatus(group, "restore", LearningPathStatus.Draft,
            "RestoreLearningPath", "Bring it back as a draft. Republishing is a second decision.");
    }

    private static void MapStatus(
        RouteGroupBuilder group,
        string route,
        LearningPathStatus status,
        string name,
        string summary) =>
        group.MapPost($"/admin/countries/{{countryCode}}/paths/{{key}}/{route}", async (
                string countryCode,
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ChangeLearningPathStatusCommand
                        {
                            CountryCode = countryCode,
                            Key = key,
                            Status = status
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManagePaths)
            .WithName(name)
            .WithSummary(summary);
}
