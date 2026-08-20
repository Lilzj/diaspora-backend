using Diaspora.Modules.Housing.Application.Analytics;
using Diaspora.Modules.Housing.Application.Moderation;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Housing.Api;

/// <summary>
/// The safety surface, and the measured record.
/// <para>
/// <b>Reporting sits at the Limited tier and survives a restriction</b>, because the person best
/// placed to flag a rental scam is often a member the platform has already restricted for something
/// unrelated — and that advert is the most damaging content this platform can carry.
/// </para>
/// <para>
/// <b>There is deliberately no member-facing read of reports.</b> Not even for a landlord to see
/// reports against their own listing: a landlord who could enumerate them could work out who filed
/// them, and a member who reported a scam must never become identifiable to the person they
/// reported.
/// </para>
/// </summary>
internal static class HousingModerationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/listings/{listingId:guid}/reports", async (
                Guid listingId,
                [FromBody] ReportHousingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new ReportHousingCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.Report)
            .WithName("ReportHousingListing")
            .WithSummary("Report a property. A scam allegation flags it immediately.");

        group.MapGet("/moderation/reports", async (
                [FromQuery] bool? openOnly,
                [FromQuery] bool? scamsFirst,
                [FromQuery] Guid? listingId,
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListHousingReportsQuery
                        {
                            OpenOnly = openOnly ?? true,
                            ScamsFirst = scamsFirst ?? true,
                            ListingId = listingId,
                            Cursor = cursor,
                            Limit = limit
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.Moderate)
            .WithName("ListHousingReports")
            .WithSummary("The housing moderation queue. Scams first.");

        group.MapPost("/moderation/reports/{reportId:guid}/resolve", async (
                Guid reportId,
                [FromBody] ResolveHousingReportRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ResolveHousingReportCommand
                        {
                            ReportId = reportId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.Moderate)
            .WithName("ResolveHousingReport")
            .WithSummary("Uphold or dismiss a report.");

        group.MapPost("/moderation/listings/{listingId:guid}", async (
                Guid listingId,
                [FromBody] ModerateHousingRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ModerateHousingCommand
                        {
                            ListingId = listingId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.Moderate)
            .WithName("ModerateHousingListing")
            .WithSummary("Hide, suspend, clear, archive or annotate a property.");

        group.MapGet("/listings/{listingId:guid}/analytics", async (
                Guid listingId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetHousingAnalyticsQuery { ListingId = listingId },
                        cancellationToken)))
            .RequireAuthorization(HousingPolicies.ViewAnalytics)
            .WithName("GetHousingAnalytics")
            .WithSummary("How a property has performed. Its landlord and staff only.");
    }
}
