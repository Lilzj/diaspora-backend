using Diaspora.Modules.Guides.Application.Intelligence.Catalogue;
using Diaspora.Modules.Guides.Application.Intelligence.Discovery;
using Diaspora.Modules.Guides.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Guides.Api;

/// <summary>
/// The notice category catalogue and the register of issuing authorities.
/// <para>
/// The reads are public and the writes are administrator-only. That split is the same one B8 makes
/// for its filing catalogue and B8.1 for its navigation one, and for the same reason: a chip row
/// every anonymous client fetches and a taxonomy editor are not the same surface, and folding them
/// together is how a permission stops being obvious.
/// </para>
/// <para>
/// <b>The category key is the address and the address is immutable.</b> It is what a seed declares,
/// what a URL carries, what every notice is filed under and what a member's notification preference
/// stores — so it is the route value, an update cannot rewrite it, and retiring a row is an archival
/// rather than a delete.
/// </para>
/// </summary>
internal static class IntelligenceCatalogueEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapCategories(group);
        MapAuthorities(group);
    }

    private static void MapCategories(RouteGroupBuilder group)
    {
        group.MapGet("/categories", async (
                string? locale,
                bool? includeArchived,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListNoticeCategoriesQuery
                        {
                            Locale = locale,
                            IncludeArchived = includeArchived ?? false
                        },
                        cancellationToken)))
            .WithName("ListNoticeCategories")
            .WithSummary("The notice category catalogue, ordered. Archived rows are editor-only.");

        group.MapPost("/categories", async (
                CreateNoticeCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateNoticeCategoryCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageCatalogue)
            .WithName("CreateNoticeCategory")
            .WithSummary("Add a kind of notice. Its key is the contract and can never change.");

        group.MapPut("/categories/{key}", async (
                string key,
                UpdateNoticeCategoryRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateNoticeCategoryCommand { Key = key, Request = request },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageCatalogue)
            .WithName("UpdateNoticeCategory")
            .WithSummary("Rewrite a category. Collections are replaced, not patched.");

        group.MapDelete("/categories/{key}", async (
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveNoticeCategoryCommand { Key = key }, cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageCatalogue)
            .WithName("ArchiveNoticeCategory")
            .WithSummary("Retire a category. Refused while any notice is still filed under it.");

        group.MapPost("/categories/{key}/restore", async (
                string key,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreNoticeCategoryCommand { Key = key }, cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageCatalogue)
            .WithName("RestoreNoticeCategory")
            .WithSummary("Return an archived category to the catalogue.");
    }

    private static void MapAuthorities(RouteGroupBuilder group)
    {
        group.MapGet("/countries/{countryCode}/authorities", async (
                string countryCode,
                string? type,
                bool? verifiedOnly,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListIssuingAuthoritiesQuery
                        {
                            CountryCode = countryCode,
                            AuthorityType = type,
                            VerifiedOnly = verifiedOnly ?? false
                        },
                        cancellationToken)))
            .WithName("ListIssuingAuthorities")
            .WithSummary("The bodies that issue for one country.");

        group.MapGet("/authorities/{authorityId:guid}", async (
                Guid authorityId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetIssuingAuthorityQuery { IssuingAuthorityId = authorityId },
                        cancellationToken)))
            .WithName("GetIssuingAuthority")
            .WithSummary("One issuing authority.");

        group.MapPost("/authorities", async (
                CreateIssuingAuthorityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateIssuingAuthorityCommand { Request = request },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageAuthorities)
            .WithName("CreateIssuingAuthority")
            .WithSummary("Record a body. It cannot publish anything until Trust verifies it.");

        group.MapPut("/authorities/{authorityId:guid}", async (
                Guid authorityId,
                UpdateIssuingAuthorityRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateIssuingAuthorityCommand
                        {
                            IssuingAuthorityId = authorityId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageAuthorities)
            .WithName("UpdateIssuingAuthority")
            .WithSummary("Rewrite an authority record. The country cannot change.");

        group.MapPost("/authorities/{authorityId:guid}/verification", async (
                Guid authorityId,
                SubmitAuthorityVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitAuthorityVerificationCommand
                        {
                            IssuingAuthorityId = authorityId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageAuthorities)
            .WithName("SubmitAuthorityVerification")
            .WithSummary(
                "Raise a claim that a body is what it says it is. Trust decides; this records.");

        group.MapDelete("/authorities/{authorityId:guid}", async (
                Guid authorityId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveIssuingAuthorityCommand { IssuingAuthorityId = authorityId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageAuthorities)
            .WithName("ArchiveIssuingAuthority")
            .WithSummary("Retire an authority. Refused while live notices still name it.");

        group.MapPost("/authorities/{authorityId:guid}/restore", async (
                Guid authorityId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreIssuingAuthorityCommand { IssuingAuthorityId = authorityId },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageAuthorities)
            .WithName("RestoreIssuingAuthority")
            .WithSummary("Return an archived authority to the register, verification intact.");
    }
}
