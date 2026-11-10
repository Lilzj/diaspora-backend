using Diaspora.Modules.Jobs.Application.Discovery;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Jobs.Api;

/// <summary>
/// Finding and reading jobs.
/// <para>
/// <b>None of these routes carries an authorisation requirement, and that is the module's whole
/// point.</b> Somebody looking for work must be able to see what work exists before anybody asks
/// them to prove who they are. Visibility is applied inside the handlers — a draft, a private
/// posting and a hidden one resolve as not found to everyone but the owning employer and staff,
/// which is a decision that belongs next to the data rather than in a route table.
/// </para>
/// <para>
/// A signed-in caller gets the same results plus the "you applied" and "you saved" annotations.
/// That is the only difference authentication makes here.
/// </para>
/// </summary>
internal static class JobDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/{jobId:guid}", async (
                Guid jobId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetJobQuery { JobId = jobId }, cancellationToken)))
            .WithName("GetJob")
            .WithSummary("One job in full, by identifier.");

        group.MapGet("/slug/{slug}", async (
                string slug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetJobQuery { Slug = slug }, cancellationToken)))
            .WithName("GetJobBySlug")
            .WithSummary("One job, by the handle a deep link carries.");

        group.MapPost("/search", async (
                [FromBody] SearchJobsRequest request,
                // Nullable: a non-nullable bool is a *required* query parameter to the minimal
                // API binder, and facets are an opt-in extra that most callers never ask for.
                [FromQuery] bool? facets,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchJobsQuery { Request = request, IncludeFacets = facets ?? false },
                        cancellationToken)))
            .RequireRateLimiting(RateLimitPolicyNames.Search)
            .WithName("SearchJobs")
            .WithSummary("Search and browse jobs. POST because the filter set is large.");

        // The same query, as a GET, for the cases a link has to carry: a shared search, a
        // category browse, a "remote jobs in Germany" deep link from a notification.
        group.MapGet("/", async (
                [AsParameters] JobBrowseParameters parameters,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchJobsQuery { Request = parameters.ToRequest() },
                        cancellationToken)))
            .RequireRateLimiting(RateLimitPolicyNames.Search)
            .WithName("BrowseJobs")
            .WithSummary("Browse jobs with the common filters, as a shareable link.");
    }
}
