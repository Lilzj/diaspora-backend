using Diaspora.Modules.Analytics.Application.Ingestion;
using Diaspora.Modules.Analytics.Application.Operations;
using Diaspora.Modules.Analytics.Application.Queries;
using Diaspora.Modules.Analytics.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using Diaspora.Platform.Modules.Contributors;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Analytics.Api;

/// <summary>
/// Maps the analytics read space: <c>/api/v1/analytics</c>.
/// <para>
/// <b>Route order is not incidental.</b> <c>/creators/me</c> and its two children are mapped before
/// <c>/content/{contentType}/{contentId}</c>, and no route in this space takes a creator identifier
/// at all — the self-scoped subject has no parameter to edit.
/// </para>
/// <para>
/// <b>Every route is a read, and every read is authorised twice.</b> The policy decides whether the
/// caller may ask; the handler decides whether they may have <em>this</em> subject. The register
/// calls an owner-scoped route whose identifier is not checked against the caller "the single most
/// likely place for an IDOR in the phase", and one check in the middleware would be exactly that.
/// </para>
/// </summary>
public sealed class AnalyticsEndpointContributor : IEndpointContributor
{
    public string Module => AnalyticsModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(AnalyticsModuleMetadata.AnalyticsRoutePrefix)
            .WithTags("Analytics");

        // ── Capability and catalogue — FRONTEND-PHASE15-GAP-025 and -030 ───────────────────

        group.MapGet("/access", async (
                string? subjectType,
                string? subjectId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetAnalyticsAccessQuery { SubjectType = subjectType, SubjectId = subjectId },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.CreatorRead)
            .WithName("GetAnalyticsAccess");

        group.MapGet("/definitions", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(new GetAnalyticsDefinitionsQuery(), cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.CreatorRead)
            .WithName("GetAnalyticsDefinitions");

        // ── Platform — FRONTEND-PHASE15-GAP-001 ────────────────────────────────────────────
        //
        // Platform-wide totals and daily additions, counted by the owning modules. Aggregate
        // counts only, so the member-scoped read gates it — see the query's own summary.
        group.MapGet("/platform", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetPlatformAnalyticsDatasetQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.CreatorRead)
            .WithName("GetPlatformAnalyticsDataset")
            .WithSummary(
                "What exists across Diaspora now, and what was added each day: guides, jobs, "
                + "listings, homes, providers and members.");

        // ── Creator — CREATOR-GAP-007 and FRONTEND-PHASE15-GAP-009 ─────────────────────────
        //
        // Two shapes over one set of figures. The dataset is the Phase 15 contract every analytics
        // screen reads; the summary is the Phase 7 contract the creator dashboard has read since it
        // shipped. Serving both is what makes this a backend change rather than a client rewrite.

        group.MapGet("/creators/me", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                bool? includeSeries,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetCreatorAnalyticsDatasetQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone,
                        IncludeSeries = includeSeries ?? false
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.CreatorRead)
            .WithName("GetCreatorAnalyticsDataset");

        // FRONTEND-PHASE15-GAP-011. Listing rollups summed to the seller, as a dataset. `/me` and
        // never `/{id}`, for the creator route's reason: a route that cannot name another seller
        // cannot leak one. `ContentRead` rather than a new permission, because a seller's listings
        // are content the caller owns and that is exactly what the existing permission grants.
        group.MapGet("/sellers/me", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                bool? includeSeries,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetSellerAnalyticsDatasetQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone,
                        IncludeSeries = includeSeries ?? false
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.ContentRead)
            .WithName("GetSellerAnalyticsDataset");

        // FRONTEND-PHASE15-GAP-013. Housing listing rollups summed to the landlord — the member, not
        // their landlord profile — as a dataset. `/me` and never `/{id}`, for the seller route's
        // reason, and `ContentRead` for the same one: a landlord's homes are content the caller owns.
        group.MapGet("/landlords/me", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                bool? includeSeries,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetLandlordAnalyticsDatasetQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone,
                        IncludeSeries = includeSeries ?? false
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.ContentRead)
            .WithName("GetLandlordAnalyticsDataset");

        // FRONTEND-PHASE15-GAP-012. Job rollups summed to the employer — the member, not their
        // employer profile — as a dataset. `/me` and never `/{id}`, and `ContentRead`, for the
        // seller and landlord routes' reasons: an employer's jobs are content the caller owns.
        group.MapGet("/employers/me", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                bool? includeSeries,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetEmployerAnalyticsDatasetQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone,
                        IncludeSeries = includeSeries ?? false
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.ContentRead)
            .WithName("GetEmployerAnalyticsDataset");

        // FRONTEND-PHASE15-GAP-010. Provider-profile rollups summed to the member who owns them, as
        // a dataset. `/me` rather than the register's `/providers/{providerId}`, and `ContentRead`,
        // for the other owner routes' reasons: a provider's profiles are content the caller owns.
        group.MapGet("/providers/me", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                bool? includeSeries,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetProviderAnalyticsDatasetQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone,
                        IncludeSeries = includeSeries ?? false
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.ContentRead)
            .WithName("GetProviderAnalyticsDataset");

        group.MapGet("/creators/me/summary", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetCreatorAnalyticsSummaryQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.CreatorRead)
            .WithName("GetCreatorAnalyticsSummary");

        group.MapGet("/creators/me/content", async (
                string? period,
                string? from,
                string? to,
                string? timezone,
                string? contentType,
                string? cursor,
                int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetCreatorContentPerformanceQuery
                    {
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone,
                        ContentType = contentType,
                        Cursor = cursor,
                        Take = take
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.ContentRead)
            .WithName("GetCreatorContentPerformance");

        // ── Content — FRONTEND-PHASE15-GAP-008 ─────────────────────────────────────────────
        //
        // One dataset serves guides, updates, events, listings, jobs and housing. §22 forbids
        // duplicating creator analytics for guides and §23 forbids a second event analytics system;
        // one route with a type segment is what honouring both looks like on the wire.

        group.MapGet("/content/{contentType}/{contentId}", async (
                string contentType,
                string contentId,
                string? period,
                string? from,
                string? to,
                string? timezone,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new GetContentAnalyticsQuery
                    {
                        ContentType = contentType,
                        ContentId = contentId,
                        Period = period,
                        From = from,
                        To = to,
                        Timezone = timezone
                    },
                    cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.ContentRead)
            .WithName("GetContentAnalytics");

        // ── Operations ─────────────────────────────────────────────────────────────────────

        group.MapPost("/operations/backfill", async (
                AnalyticsBackfillRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(
                    new StartAnalyticsBackfillCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.OperationsRun)
            .WithName("StartAnalyticsBackfill");

        group.MapGet("/operations/backfill/{runId:guid}", async (
                Guid runId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            PlatformResults.Ok(
                context,
                await sender.Send(new GetAnalyticsBackfillQuery { RunId = runId }, cancellationToken)))
            .RequireAuthorization(AnalyticsPolicies.OperationsRun)
            .WithName("GetAnalyticsBackfill");
    }
}

/// <summary>
/// Maps the telemetry ingestion space: <c>/api/v1/telemetry</c>.
/// <para>
/// <b>A separate prefix because it is a separate trust boundary.</b> Everything under
/// <c>/api/v1/analytics</c> is a read the caller must already be entitled to; this is the one write,
/// it is open to every authenticated member, and it is treated as untrusted from the first line of
/// the handler. Putting it beside the reads would have made "is this route a read" a question about
/// the path rather than about the prefix.
/// </para>
/// </summary>
public sealed class TelemetryEndpointContributor : IEndpointContributor
{
    public string Module => AnalyticsModuleMetadata.Name;

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints
            .MapGroup(AnalyticsModuleMetadata.TelemetryRoutePrefix)
            .WithTags("Telemetry");

        group.MapPost("/events", async (
                TelemetryBatchRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(
                new RecordTelemetryCommand
                {
                    Request = request,
                    CorrelationId = ReadCorrelationId(context)
                },
                cancellationToken);

            // 202 rather than 200, and the difference is the contract's meaning: the batch has been
            // accepted for processing, per-event verdicts are in the body, and nothing a client does
            // with the response should imply the figures have moved yet. Analytics is eventually
            // consistent and the status code is the first place that is said.
            return result.IsSuccess
                ? Results.Accepted(value: new ApiResponse<object>(result.Value, ApiMeta.For(context)))
                : PlatformResults.Ok(context, result);
        })
            .RequireAuthorization(AnalyticsPolicies.TelemetryWrite)
            .WithName("RecordTelemetryEvents");
    }

    /// <summary>
    /// The request's correlation identifier, when the platform's middleware set one.
    /// <para>
    /// Read from the context rather than from the body, because a client-supplied correlation
    /// identifier is a client-supplied identifier and this endpoint accepts none.
    /// </para>
    /// </summary>
    private static Guid? ReadCorrelationId(HttpContext context) =>
        Guid.TryParse(context.CorrelationId(), out var correlationId) ? correlationId : null;
}
