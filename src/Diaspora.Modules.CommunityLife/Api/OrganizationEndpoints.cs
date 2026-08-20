using Diaspora.Modules.CommunityLife.Application.Organizations;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.CommunityLife.Api;

/// <summary>
/// The organization surface.
/// <para>
/// <b>Reads by slug and by identifier are the same endpoint.</b> A body's page is linked from
/// messages years old by its slug and from the app by its identifier, and two routes would mean two
/// places to keep the visibility rules right.
/// </para>
/// </summary>
internal static class OrganizationEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapReads(group);
        MapWrites(group);
        MapMedia(group);
    }

    private static void MapReads(RouteGroupBuilder group)
    {
        group.MapGet("/organizations", async (
                [AsParameters] OrganizationSearchParameters parameters,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(parameters.ToQuery(), cancellationToken)))
            .AllowAnonymous()
            .WithName("SearchOrganizations")
            .WithSummary("The organization directory.");

        group.MapGet("/organizations/mine", async (
                [FromQuery] string? cursor,
                [FromQuery] int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMyOrganizationsQuery { Cursor = cursor, Limit = limit },
                        cancellationToken)))
            .WithName("ListMyOrganizations")
            .WithSummary("The bodies the caller runs or belongs to, drafts included.");

        group.MapGet("/organizations/{idOrSlug}", async (
                string idOrSlug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(ToGetQuery(idOrSlug), cancellationToken)))
            .AllowAnonymous()
            .WithName("GetOrganization")
            .WithSummary("One organization, by identifier or by slug.");

        group.MapGet("/organizations/{organizationId:guid}/dashboard", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetOrganizationDashboardQuery { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("GetOrganizationDashboard")
            .WithSummary("What an administrator sees when they open their organization.");
    }

    private static void MapWrites(RouteGroupBuilder group)
    {
        group.MapPost("/organizations", async (
                CreateOrganizationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new CreateOrganizationCommand { Request = request }, cancellationToken)))
            .WithName("CreateOrganization")
            .WithSummary("Drafts an organization.");

        group.MapPatch("/organizations/{organizationId:guid}", async (
                Guid organizationId,
                UpdateOrganizationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateOrganizationCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("UpdateOrganization")
            .WithSummary("Edits an organization page.");

        group.MapPost("/organizations/{organizationId:guid}/publish", async (
                Guid organizationId,
                PublishOrganizationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishOrganizationCommand
                        {
                            OrganizationId = organizationId,
                            Request = request ?? new PublishOrganizationRequest()
                        },
                        cancellationToken)))
            .WithName("PublishOrganization")
            .WithSummary("Makes an organization discoverable, or submits it for review.");

        group.MapPost("/organizations/{organizationId:guid}/archive", async (
                Guid organizationId,
                ArchiveOrganizationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ArchiveOrganizationCommand
                        {
                            OrganizationId = organizationId,
                            Request = request ?? new ArchiveOrganizationRequest()
                        },
                        cancellationToken)))
            .WithName("ArchiveOrganization")
            .WithSummary("Withdraws an organization page.");

        group.MapPost("/organizations/{organizationId:guid}/restore", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RestoreOrganizationCommand { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("RestoreOrganization")
            .WithSummary("Brings an archived or deleted organization back, as a draft.");

        group.MapDelete("/organizations/{organizationId:guid}", async (
                Guid organizationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DeleteOrganizationCommand { OrganizationId = organizationId },
                        cancellationToken)))
            .WithName("DeleteOrganization")
            .WithSummary("Soft-deletes an organization page.");

        MapDetail(group);
    }

    private static void MapDetail(RouteGroupBuilder group)
    {
        group.MapPut("/organizations/{organizationId:guid}/leaders", async (
                Guid organizationId,
                SetOrganizationLeadersRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetOrganizationLeadersCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SetOrganizationLeaders")
            .WithSummary("Replaces the leadership list.");

        group.MapPut("/organizations/{organizationId:guid}/operating-hours", async (
                Guid organizationId,
                SetOperatingHoursRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetOperatingHoursCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SetOrganizationOperatingHours")
            .WithSummary("Replaces the opening hours.");

        group.MapPost("/organizations/{organizationId:guid}/verification", async (
                Guid organizationId,
                SubmitOrganizationVerificationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SubmitOrganizationVerificationCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SubmitOrganizationVerification")
            .WithSummary("Asks Trust to verify an organization.");

        group.MapPut("/organizations/{organizationId:guid}/featured", async (
                Guid organizationId,
                SetFeaturedRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetOrganizationFeaturedCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("SetOrganizationFeatured")
            .WithSummary("Puts an organization on the front of the directory. Staff only.");
    }

    private static void MapMedia(RouteGroupBuilder group)
    {
        group.MapPost("/organizations/{organizationId:guid}/media", async (
                Guid organizationId,
                MediaAttachmentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AttachOrganizationMediaCommand
                        {
                            OrganizationId = organizationId,
                            Request = request
                        },
                        cancellationToken)))
            .WithName("AttachOrganizationMedia")
            .WithSummary("Attaches an uploaded asset to an organization page.");

        group.MapDelete("/organizations/{organizationId:guid}/media/{mediaId:guid}", async (
                Guid organizationId,
                Guid mediaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DetachOrganizationMediaCommand
                        {
                            OrganizationId = organizationId,
                            MediaId = mediaId
                        },
                        cancellationToken)))
            .WithName("DetachOrganizationMedia")
            .WithSummary("Removes an asset from an organization page.");
    }

    /// <summary>
    /// A route segment that may be either an identifier or a slug.
    /// <para>
    /// Parsed rather than routed twice: a slug that happens to look like a GUID is not a case worth
    /// two endpoints, and the handler falls back to a slug lookup when the parse fails.
    /// </para>
    /// </summary>
    private static GetOrganizationQuery ToGetQuery(string idOrSlug) =>
        Guid.TryParse(idOrSlug, out var id)
            ? new GetOrganizationQuery { OrganizationId = id }
            : new GetOrganizationQuery { Slug = idOrSlug };
}

/// <summary>The directory filters, bound from the query string.</summary>
internal sealed record OrganizationSearchParameters
{
    [FromQuery(Name = "q")]
    public string? SearchTerm { get; init; }

    [FromQuery(Name = "type")]
    public string[]? TypeKeys { get; init; }

    [FromQuery(Name = "countryCode")]
    public string? CountryCode { get; init; }

    [FromQuery(Name = "city")]
    public string? CityName { get; init; }

    [FromQuery(Name = "neighborhoodAreaId")]
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>A map viewport: <c>north,south,east,west</c>. Resolved by B4.</summary>
    [FromQuery(Name = "bounds")]
    public string? Bounds { get; init; }

    [FromQuery(Name = "verified")]
    public bool? VerifiedOnly { get; init; }

    [FromQuery(Name = "featured")]
    public bool? FeaturedOnly { get; init; }

    [FromQuery(Name = "acceptingMembers")]
    public bool? AcceptingMembers { get; init; }

    [FromQuery(Name = "language")]
    public string[]? Languages { get; init; }

    [FromQuery(Name = "sort")]
    public string? SortOrder { get; init; }

    [FromQuery(Name = "cursor")]
    public string? Cursor { get; init; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; init; }

    public SearchOrganizationsQuery ToQuery() =>
        new()
        {
            SearchTerm = SearchTerm,
            TypeKeys = TypeKeys,
            CountryCode = CountryCode,
            CityName = CityName,
            NeighborhoodAreaId = NeighborhoodAreaId,
            Bounds = Bounds,
            VerifiedOnly = VerifiedOnly,
            FeaturedOnly = FeaturedOnly,
            AcceptingMembers = AcceptingMembers,
            Languages = Languages,
            SortOrder = SortOrder,
            Cursor = Cursor,
            Limit = Limit
        };
}
