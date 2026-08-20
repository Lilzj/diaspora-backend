using Diaspora.Modules.Location.Application.Profile;
using Diaspora.Modules.Location.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Location.Api;

/// <summary>
/// The member's own geographic context.
/// <para>
/// Every route here replaces a slice of the client's Zustand store, which today does not
/// survive a reinstall and never reaches a second device.
/// </para>
/// </summary>
internal static class ProfileEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/me/profile", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetMyLocationProfileQuery(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ReadProfile)
            .WithName("GetMyLocationProfile")
            .WithSummary("The caller's location, radius, saved places, recents and travel mode.");

        group.MapPut("/me/current-location", async (
                SetCurrentLocationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SetCurrentLocationCommand { Location = request }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageProfile)
            .WithName("SetCurrentLocation")
            .WithSummary("Move the caller's active discovery location.");

        group.MapPut("/me/preferences", async (
                UpdateGeographicPreferencesRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new UpdateGeographicPreferencesCommand { Preferences = request },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageProfile)
            .WithName("UpdateGeographicPreferences")
            .WithSummary("Change radius, distance unit, coordinate precision and default places.");

        group.MapPut("/me/travel-mode", async (
                SetTravelModeRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SetTravelModeCommand
                    {
                        Enabled = request.Enabled,
                        Destination = request.Destination
                    },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageProfile)
            .WithName("SetTravelMode")
            .WithSummary("Discover where you are travelling without losing where you live.");

        // Label in the path rather than the body: it is the resource's identity within the
        // profile, and PUT-to-a-label is what makes saving "Home" twice a move rather than a
        // duplicate.
        group.MapPut("/me/saved-locations/{label}", async (
                string label,
                SaveLocationRequest? request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new SaveLocationCommand { Label = label, Location = request?.Location },
                    cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageProfile)
            .WithName("SaveLocation")
            .WithSummary("Save a labelled place, or move one. Omit the body to save where you are.");

        group.MapDelete("/me/saved-locations/{label}", async (
                string label, ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new RemoveSavedLocationCommand { Label = label }, cancellationToken);

                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageProfile)
            .WithName("RemoveSavedLocation")
            .WithSummary("Forget a labelled place.");

        group.MapDelete("/me/recent-locations", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new ClearRecentLocationsCommand(), cancellationToken);
                return PlatformResults.Ok(context, result);
            })
            .RequireAuthorization(LocationPolicies.ManageProfile)
            .WithName("ClearRecentLocations")
            .WithSummary("Clear the caller's recent-locations list.");
    }
}
