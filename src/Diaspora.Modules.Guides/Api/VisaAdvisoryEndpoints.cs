using Diaspora.Modules.Guides.Application.Intelligence.Directory;
using Diaspora.Modules.Guides.Application.Intelligence.Discovery;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Visa and immigration advisories.
/// <para>
/// <b><c>appliesTo</c> is the parameter that makes this surface useful.</b> It matches advisories
/// naming that nationality <em>and</em> those naming nobody, because both apply — a listing that
/// matched only the exact code would tell most members that no rules exist for them, which is both
/// wrong and the most dangerous kind of wrong this module can be.
/// </para>
/// <para>
/// There is nothing here about applying. Application processing is explicitly out of scope, and the
/// route table is where that is visible to every client author who might otherwise assume it is
/// coming.
/// </para>
/// </summary>
internal static class VisaAdvisoryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/countries/{countryCode}/visas", async (
                string countryCode,
                string? appliesTo,
                bool? activeOnly,
                string? cursor,
                int? limit,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListVisaAdvisoriesQuery
                        {
                            CountryCode = countryCode,
                            AppliesTo = appliesTo,
                            ActiveOnly = activeOnly ?? true,
                            Cursor = cursor,
                            Limit = limit,
                            Locale = locale
                        },
                        cancellationToken)))
            .WithName("ListVisaAdvisories")
            .WithSummary(
                "Entry rules for a destination. Supplying a nationality also returns the rules " +
                "that apply to everybody.");

        group.MapGet("/visas/{advisoryId:guid}", async (
                Guid advisoryId,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetVisaAdvisoryQuery
                        {
                            VisaAdvisoryId = advisoryId,
                            Locale = locale
                        },
                        cancellationToken)))
            .WithName("GetVisaAdvisory")
            .WithSummary("One advisory: requirements, documents, processing time and where to apply.");

        group.MapPost("/visas", async (
                CreateVisaAdvisoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateVisaAdvisoryCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageVisas)
            .WithName("CreateVisaAdvisory")
            .WithSummary("Open a draft advisory. The authority must issue for the destination.");

        group.MapPut("/visas/{advisoryId:guid}", async (
                Guid advisoryId,
                UpdateVisaAdvisoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateVisaAdvisoryCommand
                        {
                            VisaAdvisoryId = advisoryId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageVisas)
            .WithName("UpdateVisaAdvisory")
            .WithSummary(
                "Rewrite an advisory. Editable while published — the history lives in the notices.");

        group.MapPost("/visas/{advisoryId:guid}/publish", async (
                Guid advisoryId,
                int? expectedRevision,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishVisaAdvisoryCommand
                        {
                            VisaAdvisoryId = advisoryId,
                            ExpectedRevision = expectedRevision
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageVisas)
            .WithName("PublishVisaAdvisory")
            .WithSummary("Make an advisory visible. The authority must be verified.");

        group.MapDelete("/visas/{advisoryId:guid}", async (
                Guid advisoryId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveVisaAdvisoryCommand { VisaAdvisoryId = advisoryId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageVisas)
            .WithName("ArchiveVisaAdvisory")
            .WithSummary("Withdraw an advisory. A soft delete; restorable.");

        group.MapPost("/visas/{advisoryId:guid}/restore", async (
                Guid advisoryId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreVisaAdvisoryCommand { VisaAdvisoryId = advisoryId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageVisas)
            .WithName("RestoreVisaAdvisory")
            .WithSummary("Return an archived advisory to the desk as a draft.");
    }
}
