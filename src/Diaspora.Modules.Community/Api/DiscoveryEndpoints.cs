using Diaspora.Modules.Community.Application.Capabilities;
using Diaspora.Modules.Community.Application.Hashtags;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Community.Api;

/// <summary>Hashtags, trending, and what this deployment can do.</summary>
internal static class DiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/hashtags/suggest", async (
                string prefix,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SuggestHashtagsQuery { Prefix = prefix, Limit = limit ?? 10 },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("SuggestCommunityHashtags")
            .WithSummary("Autocomplete for the composer.");

        group.MapGet("/hashtags/trending", async (
                string? countryCode,
                int? limit,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ListTrendingHashtagsQuery
                        {
                            CountryCode = countryCode, Limit = limit ?? 10
                        },
                        cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("ListCommunityTrendingHashtags")
            .WithSummary("What is trending, optionally in one country.");

        group.MapGet("/capabilities", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetCapabilitiesQuery(), cancellationToken)))
            .RequireAuthorization(CommunityPolicies.Read)
            .WithName("GetCommunityCapabilities")
            .WithSummary("Reactions, feeds, rankings, reasons and limits, live.");
    }
}
