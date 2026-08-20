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
/// The embassy directory.
/// <para>
/// <b><c>locatedIn</c> and <c>serving</c> are separate query parameters and neither defaults to the
/// other.</b> "Missions in the United Kingdom" and "missions serving British nationals" are different
/// lists with almost no overlap, and a single <c>country</c> parameter would answer whichever the
/// last caller had in mind. A client that wants both sends both.
/// </para>
/// </summary>
internal static class EmbassyEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/embassies", async (
                string? locatedIn,
                string? serving,
                bool? verifiedOnly,
                string? q,
                string? cursor,
                int? limit,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListEmbassiesQuery
                        {
                            LocatedIn = locatedIn,
                            Serving = serving,
                            VerifiedOnly = verifiedOnly ?? false,
                            Text = q,
                            Cursor = cursor,
                            Limit = limit,
                            Locale = locale
                        },
                        cancellationToken)))
            .WithName("ListEmbassies")
            .WithSummary(
                "The embassy directory. Filter by where a mission is, whom it serves, or both.");

        group.MapGet("/embassies/{embassyId:guid}", async (
                Guid embassyId,
                string? locale,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetEmbassyQuery { EmbassyId = embassyId, Locale = locale },
                        cancellationToken)))
            .WithName("GetEmbassy")
            .WithSummary(
                "One mission: hours, services, the appointment link and the emergency line.");

        group.MapPost("/embassies", async (
                CreateEmbassyRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateEmbassyCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("CreateEmbassy")
            .WithSummary("Add a mission. Neither country code can change afterwards.");

        group.MapPut("/embassies/{embassyId:guid}", async (
                Guid embassyId,
                UpdateEmbassyRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateEmbassyCommand { EmbassyId = embassyId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("UpdateEmbassy")
            .WithSummary("Rewrite a mission's entry. Collections are replaced, not patched.");

        group.MapPost("/embassies/{embassyId:guid}/verification", async (
                Guid embassyId,
                SubmitAuthorityVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitEmbassyVerificationCommand
                        {
                            EmbassyId = embassyId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageAuthorities)
            .WithName("SubmitEmbassyVerification")
            .WithSummary("Raise a claim that a mission is what it says it is.");

        group.MapDelete("/embassies/{embassyId:guid}", async (
                Guid embassyId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveEmbassyCommand { EmbassyId = embassyId }, cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("ArchiveEmbassy")
            .WithSummary("Remove a mission from the directory. A soft delete; restorable.");

        group.MapPost("/embassies/{embassyId:guid}/restore", async (
                Guid embassyId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreEmbassyCommand { EmbassyId = embassyId }, cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("RestoreEmbassy")
            .WithSummary("Return an archived mission to the directory, verification intact.");
    }
}
