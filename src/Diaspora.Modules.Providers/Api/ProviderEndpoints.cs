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
            .WithSummary("Publish, pause, close, suspend, reinstate or archive.");
    }

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
