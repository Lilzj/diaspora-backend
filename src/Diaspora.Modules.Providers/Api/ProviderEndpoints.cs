using Diaspora.Modules.Providers.Application.Capabilities;
using Diaspora.Modules.Providers.Application.Discovery;
using Diaspora.Modules.Providers.Application.Management;
using Diaspora.Modules.Providers.Application.Registration;
using Diaspora.Modules.Providers.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Providers.Api;

/// <summary>
/// Discovery, profile reads and the provider lifecycle.
/// <para>
/// Every route dispatches through <c>ISender</c> so the shared pipeline runs — validation,
/// telemetry, transactions, authorisation and audit — and returns through
/// <c>PlatformResults</c> so the envelope and problem documents are byte-for-byte identical to
/// Identity's controllers and to Trust's, Location's, Media's and Community's endpoints.
/// </para>
/// <para>
/// <b>The search endpoint takes its filters from the query string, not a body.</b> A GET with a
/// body is unusable from a browser, uncacheable by any intermediary, and unlinkable — and a
/// discovery result that cannot be shared as a URL is the one thing a directory most needs to
/// be shareable.
/// </para>
/// </summary>
internal static class ProviderEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        MapDiscovery(group);
        MapProfile(group);
        MapLifecycle(group);
    }

    private static void MapDiscovery(RouteGroupBuilder group)
    {
        group.MapGet("/", async (
                [AsParameters] ProviderSearchRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchProvidersQuery
                        {
                            Request = request,
                            LanguageCode = LanguageOf(context)
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("SearchProviders")
            .WithSummary("Find providers, with every discovery filter the client offers.");

        // Declared before `/{idOrSlug}` would be reached, so the literal `me` resolves this
        // route rather than being read as a slug — the same ordering `/placements/me` needs in
        // Financial, and for the same reason.
        group.MapGet("/me", async (
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListMyProvidersQuery
                        {
                            Limit = limit,
                            LanguageCode = LanguageOf(context)
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("ListMyProviders")
            .WithSummary("Every provider the caller holds a role on.");

        group.MapGet("/featured", async (
                [AsParameters] ProviderSearchRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SearchProvidersQuery
                        {
                            Request = request with { FeaturedOnly = true },
                            LanguageCode = LanguageOf(context)
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("ListFeaturedProviders")
            .WithSummary("The featured carousel.");

        group.MapGet("/capabilities", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetProviderCapabilitiesQuery(), cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("GetProviderCapabilities")
            .WithSummary("What this deployment offers: types, models, limits, ranking keys.");
    }

    private static void MapProfile(RouteGroupBuilder group)
    {
        group.MapGet("/{providerId:guid}", async (
                Guid providerId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetProviderQuery
                        {
                            ProviderId = providerId,
                            LanguageCode = LanguageOf(context)
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("GetProvider")
            .WithSummary("One provider in full.");

        group.MapGet("/slug/{slug}", async (
                string slug,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetProviderQuery { Slug = slug, LanguageCode = LanguageOf(context) },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Read)
            .WithName("GetProviderBySlug")
            .WithSummary("One provider, by its deep-link handle.");
    }

    private static void MapLifecycle(RouteGroupBuilder group)
    {
        group.MapPost("/", async (
                RegisterProviderRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Created(
                    context,
                    await sender.Send(
                        new RegisterProviderCommand { Request = request }, cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Register)
            .WithName("RegisterProvider")
            .WithSummary("Register a provider. It is created as a draft.");

        group.MapPut("/{providerId:guid}", async (
                Guid providerId,
                UpdateProviderRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateProviderCommand { ProviderId = providerId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Manage)
            .WithName("UpdateProvider")
            .WithSummary("Edit a provider's name, type, category and delivery mode.");

        group.MapPut("/{providerId:guid}/status", async (
                Guid providerId,
                ProviderStatusRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ChangeProviderStatusCommand
                        {
                            ProviderId = providerId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.ManageStatus)
            .WithName("ChangeProviderStatus")
            .WithSummary(
                "Publish, pause, close or archive. The owner's route: staff act on a provider only " +
                "through its Trust moderation case.");

        // Staff suspended and reinstated a provider through POST /providers/moderation/{providerId},
        // added by stage 2 of the owner's staff rule (7 Nov 2026). Since stage 5 they act on a
        // member's provider only by deciding its Trust moderation case
        // (/trust/moderation/queue/{id}/decision), which carries the decision out through
        // ProviderModerationTarget. Featuring, below, is curation and stays here.
        group.MapPut("/{providerId:guid}/featured", async (
                Guid providerId,
                SetProviderFeaturedRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetProviderFeaturedCommand
                        {
                            ProviderId = providerId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Administer)
            .WithName("SetProviderFeatured")
            .WithSummary(
                "Put a provider into the featured carousel, or take it out. Editorial, and the " +
                "only thing that fills GET /providers/featured.");

        MapServices(group);
        MapAvailability(group);
    }

    /// <summary>
    /// What a provider offers.
    /// <para>
    /// Separate routes rather than a services array on <c>UpdateProviderRequest</c>, because a
    /// restaurant editing one dish must not have to resend its whole menu — a client that has to
    /// send the full list to change one row will eventually send a stale one and silently delete
    /// the rest.
    /// </para>
    /// <para>
    /// All three answer with the whole <c>ProviderResponse</c>, not the single service. The
    /// aggregate recalculates its service count, display ordering and profile-completion score on
    /// every one of these, so returning just the row would leave the client holding figures that
    /// are already wrong.
    /// </para>
    /// </summary>
    private static void MapServices(RouteGroupBuilder group)
    {
        group.MapPatch("/{providerId:guid}/details", async (
                Guid providerId,
                UpdateProviderDetailsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateProviderDetailsCommand { ProviderId = providerId, Request = request },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.Manage)
            .WithName("UpdateProviderDetails")
            .WithSummary("Change a provider's description, contact methods or location. Partial.");

        group.MapDelete("/{providerId:guid}/photos/{mediaId:guid}", async (
                Guid providerId,
                Guid mediaId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RemoveProviderPhotoCommand { ProviderId = providerId, MediaId = mediaId },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.ManageMedia)
            .WithName("RemoveProviderPhoto")
            .WithSummary("Take a photograph off a provider.");

        group.MapPost("/{providerId:guid}/photos", async (
                Guid providerId,
                ProviderGalleryItemRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AddProviderPhotoCommand
                        {
                            ProviderId = providerId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.ManageMedia)
            .WithName("AddProviderPhoto")
            .WithSummary("Put an uploaded photograph on a provider, as its cover or in its gallery.");

        group.MapPost("/{providerId:guid}/services", async (
                Guid providerId,
                ProviderServiceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AddProviderServiceCommand
                        {
                            ProviderId = providerId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.ManageServices)
            .WithName("AddProviderService")
            .WithSummary("Add something a provider offers.");

        group.MapPut("/{providerId:guid}/services/{serviceId:guid}", async (
                Guid providerId,
                Guid serviceId,
                ProviderServiceRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateProviderServiceCommand
                        {
                            ProviderId = providerId,
                            ServiceId = serviceId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.ManageServices)
            .WithName("UpdateProviderService")
            .WithSummary("Edit something a provider offers.");

        group.MapDelete("/{providerId:guid}/services/{serviceId:guid}", async (
                Guid providerId,
                Guid serviceId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RemoveProviderServiceCommand
                        {
                            ProviderId = providerId,
                            ServiceId = serviceId
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.ManageServices)
            .WithName("RemoveProviderService")
            .WithSummary("Remove something a provider offers.");
    }

    /// <summary>
    /// When a provider is open.
    /// <para>
    /// One route taking a list, rather than seven per-day calls, because the client's hours
    /// editor is a week of rows saved as one screen. The command is <c>[Transactional]</c>, so
    /// either the whole week lands or none of it does — a half-saved schedule is a business
    /// advertising hours it never agreed to.
    /// </para>
    /// <para>
    /// Days the request omits are left as they were. Closing a day is
    /// <c>OperatingHoursRequest.IsClosed</c>, which says "we do not open Sundays"; omission says
    /// nothing at all, and the two must not collapse into each other.
    /// </para>
    /// </summary>
    private static void MapAvailability(RouteGroupBuilder group) =>
        group.MapPut("/{providerId:guid}/operating-hours", async (
                Guid providerId,
                SetProviderOperatingHoursRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetProviderOperatingHoursCommand
                        {
                            ProviderId = providerId,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(ProviderPolicies.ManageAvailability)
            .WithName("SetProviderOperatingHours")
            .WithSummary("Replace a provider's opening hours for the days given.");

    /// <summary>
    /// The caller's preferred language, for category translation.
    /// <para>
    /// Read from <c>Accept-Language</c> and reduced to the first tag. Full RFC 4647 negotiation
    /// would be the correct thing and is overkill here: B7 has one translated field per category
    /// and falls back on the primary subtag anyway, so "the first tag the client asked for" is
    /// the whole of what it can act on.
    /// </para>
    /// </summary>
    private static string? LanguageOf(HttpContext context)
    {
        var header = context.Request.Headers.AcceptLanguage.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var first = header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        return first?.Split(';')[0].Trim();
    }
}
