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
/// One country's emergency information.
/// <para>
/// <b>Two routes, and the read is the most important endpoint this module serves.</b> It is
/// anonymous, it needs no other call to render, and its response is shaped so a client can cache it
/// offline — which is exactly what a client should do with it, because the moment somebody needs this
/// is the moment their connection is least likely to be working.
/// </para>
/// <para>
/// <b>The write is a single save of the whole page</b>, because that is how it is edited: somebody
/// checks the whole thing against an official source and saves it. There is no route to add one
/// number, deliberately.
/// </para>
/// </summary>
internal static class EmergencyEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/countries/{countryCode}/emergency", async (
                string countryCode,
                string? locale,
                bool? recordView,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetEmergencyContactsQuery
                        {
                            CountryCode = countryCode,
                            Locale = locale,
                            RecordView = recordView ?? true
                        },
                        cancellationToken)))
            .WithName("GetEmergencyContacts")
            .WithSummary(
                "Every number somebody in trouble should be able to find. Anonymous, and safe to " +
                "cache offline.");

        group.MapPut("/countries/{countryCode}/emergency", async (
                string countryCode,
                SaveEmergencyContactsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SaveEmergencyContactsCommand
                        {
                            CountryCode = countryCode,
                            Request = request
                        },
                        cancellationToken)))
            .RequireAuthorization(IntelligencePolicies.ManageDirectory)
            .WithName("SaveEmergencyContacts")
            .WithSummary(
                "Save the whole page, creating it on first save. A repeated kind is refused.");
    }
}
