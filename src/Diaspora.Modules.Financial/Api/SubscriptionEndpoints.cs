using Diaspora.Modules.Financial.Application.Subscriptions;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>Plans and subscriptions.</summary>
internal static class SubscriptionEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/subscriptions/plans", async (
                [FromQuery] string? audience,
                [FromQuery] string? country,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetSubscriptionPlansQuery
                        {
                            AudienceType = audience,
                            CountryCode = country
                        },
                        cancellationToken)))
            .WithName("GetSubscriptionPlans")
            .WithSummary("The plan catalogue. Render the entitlements, never the plan name.");

        group.MapGet("/subscriptions/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetMySubscriptionQuery(), cancellationToken)))
            .WithName("GetMySubscription")
            .WithSummary("The caller's subscription, or null for the free plan.");

        group.MapPost("/subscriptions", async (
                ActivateSubscriptionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ActivateSubscriptionCommand
                        {
                            PlanCode = request.PlanCode,
                            WithTrial = request.WithTrial,
                            MethodKind = request.MethodKind,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey
                        },
                        cancellationToken)))
            .WithName("ActivateSubscription")
            .WithSummary("Starts a subscription. A trial is refused to a returning subscriber.");

        group.MapPost("/subscriptions/{subscriptionId:guid}/cancel", async (
                Guid subscriptionId,
                CancelSubscriptionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelSubscriptionCommand
                        {
                            SubscriptionId = subscriptionId,
                            Reason = request.Reason,
                            Immediately = request.Immediately
                        },
                        cancellationToken)))
            .WithName("CancelSubscription")
            .WithSummary("Stops it renewing. Access continues to the end of the paid period.");

        group.MapGet("/subscriptions/{subscriptionId:guid}/change/preview", async (
                Guid subscriptionId,
                [FromQuery] string planCode,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PreviewSubscriptionChangeQuery
                        {
                            SubscriptionId = subscriptionId,
                            PlanCode = planCode
                        },
                        cancellationToken)))
            .WithName("PreviewSubscriptionChange")
            .WithSummary(
                "What changing plan would cost and change. Server-computed — a client multiplying "
                + "a price by a remaining fraction disagrees at the first rounding boundary.");

        group.MapPost("/subscriptions/{subscriptionId:guid}/change", async (
                Guid subscriptionId,
                ChangeSubscriptionPlanRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ChangeSubscriptionPlanCommand
                        {
                            SubscriptionId = subscriptionId,
                            PlanCode = request.PlanCode,
                            MethodKind = request.MethodKind,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey
                        },
                        cancellationToken)))
            .WithName("ChangeSubscriptionPlan")
            .WithSummary(
                "An upgrade takes effect now and charges the difference; a downgrade waits for the "
                + "period boundary, because somebody who paid for the month keeps the month.");

        group.MapGet("/subscriptions/me/upcoming", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetUpcomingChargesQuery(), cancellationToken)))
            .WithName("GetUpcomingCharges")
            .WithSummary("The next charge, priced by the server rather than forecast on the device.");
    }
}
