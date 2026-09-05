using Diaspora.Modules.Creator.Application.Earnings;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Creator.Api;

/// <summary>
/// What a creator earned, and what it was for — CREATOR-GAP-008.
/// <para>
/// <b>Reads only.</b> Earnings are recorded by consumers from payments the platform already
/// published; nothing here creates one. A route that let a caller declare an earning would be a
/// second, unauthenticated revenue model.
/// </para>
/// <para>
/// <b>Both routes are <c>/me</c>-scoped and neither takes a creator id.</b> One creator's income is
/// not another's business, and an id parameter would make "can I read this" a question the handler
/// has to answer correctly every time rather than one the route cannot ask.
/// </para>
/// </summary>
public static class CreatorEarningEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/me/earnings", async (
                [FromQuery] string? state,
                [FromQuery] string? source,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyEarningsQuery { State = state, Source = source, Take = take },
                        cancellationToken)))
            .WithName("GetMyCreatorEarnings")
            .WithSummary(
                "The caller's own earnings, newest first, each with the source it came from, the "
                + "state it is in, and both the share and what was actually paid.");

        group.MapGet("/me/revenue/summary", async (
                [FromQuery] int? days,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyRevenueSummaryQuery { Days = days }, cancellationToken)))
            .WithName("GetMyCreatorRevenueSummary")
            .WithSummary(
                "Lifetime, pending, available, processing and paid, plus the per-source breakdown. "
                + "Summed in one currency, and says so when there are others.");
    }
}
