using Diaspora.Modules.Guides.Application.Knowledge.Directories;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// Curating one country's directory: opening it, arranging it, publishing it, retiring it.
/// <para>
/// Every route here is administrator-only and sits under <c>/admin</c>, apart from the read that
/// serves the curation screen itself. The separation from the reader-facing country page is
/// deliberate: they return different contracts to different audiences, and a single route whose
/// response shape depended on who was asking would make every consumer's rendering conditional on
/// authentication.
/// </para>
/// <para>
/// <b>Publish, unpublish, archive and restore are four routes rather than a status field.</b> A
/// status a client can set is a status a client can set wrongly — and these four are genuinely
/// different acts: two flip a flag, one goes through the platform's soft-delete path, and one comes
/// back from it.
/// </para>
/// </summary>
internal static class KnowledgeDirectoryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/admin/countries/{countryCode}", async (
                string countryCode,
                bool? includeArchived,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetCountryDirectoryDetailQuery
                        {
                            CountryCode = countryCode,
                            IncludeArchived = includeArchived ?? false
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("GetCountryDirectoryDetail")
            .WithSummary("The curation screen: hidden rows, unpublished state, translations.");

        group.MapPost("/admin/countries/{countryCode}", async (
                string countryCode,
                CreateCountryDirectoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateCountryDirectoryCommand
                        {
                            CountryCode = countryCode,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("CreateCountryDirectory")
            .WithSummary("Open a country. Arranged from a template, published separately.");

        group.MapPut("/admin/countries/{countryCode}", async (
                string countryCode,
                UpdateCountryDirectoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateCountryDirectoryCommand
                        {
                            CountryCode = countryCode,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("UpdateCountryDirectory")
            .WithSummary("Rewrite the header copy and its translations.");

        group.MapPut("/admin/countries/{countryCode}/arrangement", async (
                string countryCode,
                ArrangeCountryDirectoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArrangeCountryDirectoryCommand
                        {
                            CountryCode = countryCode,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("ArrangeCountryDirectory")
            .WithSummary("Replace the whole arrangement. Position comes from the list order.");

        MapLifecycle(group);
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        group.MapPost("/admin/countries/{countryCode}/publish", async (
                string countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishCountryDirectoryCommand { CountryCode = countryCode },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("PublishCountryDirectory")
            .WithSummary("Make the country visible to readers.");

        group.MapPost("/admin/countries/{countryCode}/unpublish", async (
                string countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UnpublishCountryDirectoryCommand { CountryCode = countryCode },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("UnpublishCountryDirectory")
            .WithSummary("Take it off the reader surface. The arrangement stays.");

        group.MapDelete("/admin/countries/{countryCode}", async (
                string countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveCountryDirectoryCommand { CountryCode = countryCode },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("ArchiveCountryDirectory")
            .WithSummary("Retire the country entirely. Soft, so it can come back.");

        group.MapPost("/admin/countries/{countryCode}/restore", async (
                string countryCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreCountryDirectoryCommand { CountryCode = countryCode },
                        cancellationToken)))
            .RequireAuthorization(KnowledgePolicies.ManageDirectory)
            .WithName("RestoreCountryDirectory")
            .WithSummary("Bring it back to the desk, unpublished. Publishing is a second decision.");
    }
}
