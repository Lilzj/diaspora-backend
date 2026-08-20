using Diaspora.Modules.Financial.Application.Campaigns;
using Diaspora.Modules.Financial.Application.Giving;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// B15.1's own routes: the giving time series and recurring donations.
/// <para>
/// <b>The campaign routes are deliberately not here.</b> The register puts them under
/// <c>/api/v1/communities</c>, which is another module's URL space and this platform allows exactly
/// one owner per prefix. The campaign aggregate, its state machine and every decision about it stay
/// in this module; the routes are mapped by the module that owns that prefix and dispatch this
/// module's commands — the same split B18 already uses to expose a Community Life write. Ownership
/// follows the aggregate, not the URL.
/// </para>
/// </summary>
internal static class GivingExtensionEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/donations/causes/{causeCode}/timeseries", async (
                string causeCode,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetDonationTimeSeriesQuery
                        {
                            CauseCode = causeCode,
                            From = from,
                            To = to
                        },
                        cancellationToken)))
            .WithName("GetDonationTimeSeries")
            .WithSummary(
                "A cause's giving, bucketed by UTC day. Every day in the window is present, "
                + "including the empty ones.");

        group.MapGet("/donations/recurring/me", async (
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyRecurringDonationsQuery(), cancellationToken)))
            .WithName("GetMyRecurringDonations")
            .WithSummary("The caller's standing donations, with the next charge date. Every state.");

        group.MapPost("/donations/recurring/{id:guid}/cancel", async (
                Guid id,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelRecurringDonationCommand { RecurringDonationId = id },
                        cancellationToken)))
            .WithName("CancelRecurringDonation")
            .WithSummary("Stops a standing donation. Owner only, and idempotent.");

        group.MapGet("/memberships/{membershipId:guid}/payments", async (
                Guid membershipId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new Application.Memberships.GetMembershipPaymentsQuery
                        {
                            MembershipId = membershipId
                        },
                        cancellationToken)))
            .WithName("GetMembershipPayments")
            .WithSummary(
                "Every settled payment bound to one membership. The financial half of the "
                + "membership record, read from the module that owns the money.");
    }
}
