using Diaspora.Modules.Financial.Application.Rewards;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Reward points, and the one place they become money.
/// <para>
/// <b>The balance route serves points and the conversion route serves money, and nothing serves
/// both.</b> A point is a count of something the platform chose to recognise; what it is worth is a
/// policy the Financial Platform re-reads every time it is asked. Nothing outside this module
/// converts, and no response on the balance surface carries a currency.
/// </para>
/// <para>
/// <b>The rate is served, and the preview is still a request.</b> §12 of the frontend's brief forbids
/// computing a conversion rate locally when the backend owns the rule; serving the rate makes obeying
/// that possible, and making the preview a round trip is what keeps the server the only thing that
/// decides what a member gets.
/// </para>
/// </summary>
internal static class RewardEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/rewards/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetMyRewardBalanceQuery(), cancellationToken)))
            .WithName("GetMyRewardBalance")
            .WithSummary("The caller's points. No currency on this response, ever.");

        // `excludeIds`, like every other paged read in this module — including
        // `/rewards/conversions` below. Without it the boundary instant was re-served whole on
        // every page.
        group.MapGet("/rewards/me/history", async (
                [FromQuery] DateTimeOffset? before,
                [FromQuery] Guid[]? excludeIds,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyRewardHistoryQuery
                        {
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyRewardHistory")
            .WithSummary("How the caller earned and spent their points. Cursor-paged.");

        group.MapGet("/rewards/conversion", async (
                [FromQuery] string? currency,
                [FromQuery] string? country,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRewardConversionTermsQuery
                        {
                            Currency = currency,
                            CountryCode = country
                        },
                        cancellationToken)))
            .WithName("GetRewardConversionTerms")
            .WithSummary("The rate, the minimum, the maximum and the daily cap. From policy.");

        group.MapPost("/rewards/conversions/preview", async (
                ConvertRewardPointsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PreviewRewardConversionQuery
                        {
                            Points = request.Points,
                            Currency = request.Currency,
                            CountryCode = request.CountryCode
                        },
                        cancellationToken)))
            .WithName("PreviewRewardConversion")
            .WithSummary("What a conversion would produce. Priced by the server, not the client.");

        group.MapPost("/rewards/conversions", async (
                ConvertRewardPointsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ConvertRewardPointsCommand
                        {
                            Points = request.Points,
                            Currency = request.Currency,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = IdempotencyHeader.Resolve(context, request.IdempotencyKey)
                        },
                        cancellationToken)))
            .WithName("ConvertRewardPoints")
            .WithSummary("Turns points into credit. Idempotent, and posts a real ledger entry.");

        group.MapGet("/rewards/conversions", async (
                [FromQuery] DateTimeOffset? before,
                [FromQuery] Guid[]? excludeIds,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyRewardConversionsQuery
                        {
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyRewardConversions")
            .WithSummary("Every crossing the caller has made, with the rate that was applied.");

        group.MapPost("/operations/rewards/grants", async (
                GrantRewardPointsRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GrantRewardPointsCommand
                        {
                            UserId = request.UserId,
                            Points = request.Points,
                            Source = request.Source,
                            GrantReference = request.GrantReference,
                            Reason = request.Reason
                        },
                        cancellationToken)))
            .WithName("GrantRewardPoints")
            .WithSummary("Awards points, once per grant reference. Staff.");

        group.MapPut("/operations/rewards/members/{userId:guid}/conversion-hold", async (
                Guid userId,
                SetRewardConversionHoldRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new SetRewardConversionHoldCommand
                        {
                            UserId = userId,
                            IsHeld = request.IsHeld,
                            Reason = request.Reason
                        },
                        cancellationToken)))
            .WithName("SetRewardConversionHold")
            .WithSummary("Holds a member's points from conversion, or lifts the hold, and says why. Staff.");
    }
}
