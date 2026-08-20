using Diaspora.Modules.Financial.Application.Operations;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// The operator's surface, and the two things a client needs to render money.
/// <para>
/// <c>/currencies</c> and <c>/capabilities</c> are member-readable on purpose: a client needs the
/// symbol and the exponent to render an amount, and needs to know which payment methods this
/// deployment actually supports before it draws a payment sheet.
/// </para>
/// </summary>
internal static class FinancialOperationsEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/currencies", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context, await sender.Send(new GetCurrenciesQuery(), cancellationToken)))
            .WithName("GetFinancialCurrencies")
            .WithSummary("Every currency the platform formats, with its symbol and exponent.");

        group.MapGet("/capabilities", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetFinancialCapabilitiesQuery(), cancellationToken)))
            .WithName("GetFinancialCapabilities")
            .WithSummary("What this deployment can do. Conversion and payouts report false.");

        group.MapGet("/operations/policies", async (
                [FromQuery] string? key,
                [FromQuery] bool? includeInactive,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetFinancialPoliciesQuery
                        {
                            PolicyKey = key,
                            IncludeInactive = includeInactive ?? false
                        },
                        cancellationToken)))
            .WithName("GetFinancialPolicies")
            .WithSummary("The versioned policy catalogue. Staff.");

        group.MapPost("/operations/policies", async (
                PublishFinancialPolicyRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new PublishFinancialPolicyCommand
                        {
                            PolicyKey = request.PolicyKey,
                            Kind = request.Kind,
                            Scope = request.Scope,
                            ScopeValue = request.ScopeValue,
                            Description = request.Description,
                            Parameters = request.Parameters,
                            EffectiveFrom = request.EffectiveFrom
                        },
                        cancellationToken)))
            .WithName("PublishFinancialPolicy")
            .WithSummary("Publishes a new version. Never an edit. Staff.");

        group.MapPut("/operations/catalog", async (
                UpsertMonetizationProductRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpsertMonetizationProductCommand
                        {
                            Code = request.Code,
                            Name = request.Name,
                            Description = request.Description,
                            Category = request.Category,
                            SubjectType = request.SubjectType,
                            PriceAmount = request.PriceAmount,
                            Currency = request.Currency,
                            PricingPolicyKey = request.PricingPolicyKey,
                            DurationDays = request.DurationDays,
                            PlacementKind = request.PlacementKind,
                            PriorityWeight = request.PriorityWeight,
                            IsRenewable = request.IsRenewable,
                            SortOrder = request.SortOrder,
                            Eligibility = request.Eligibility,
                            Activate = request.Activate,
                            AvailableFrom = request.AvailableFrom,
                            AvailableTo = request.AvailableTo
                        },
                        cancellationToken)))
            .WithName("UpsertMonetizationProduct")
            .WithSummary("Adds or changes a product. The only way a premium feature exists. Staff.");

        group.MapPost("/operations/credit", async (
                GrantCreditRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GrantCreditCommand
                        {
                            UserId = request.UserId,
                            Amount = request.Amount,
                            Currency = request.Currency,
                            Kind = request.Kind,
                            Reason = request.Reason,
                            ExpiresAt = request.ExpiresAt
                        },
                        cancellationToken)))
            .WithName("GrantCredit")
            .WithSummary("Grants platform credit into a promotional wallet. Staff.");

        group.MapGet("/operations/dashboard", async (
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetFinancialDashboardQuery { From = from, To = to },
                        cancellationToken)))
            .WithName("GetFinancialDashboard")
            .WithSummary("The operator's overview. Per currency, never totalled across them. Staff.");

        group.MapGet("/operations/revenue", async (
                [FromQuery] DateTimeOffset from,
                [FromQuery] DateTimeOffset to,
                [FromQuery] string? dimension,
                [FromQuery] string? currency,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetRevenueSummaryQuery
                        {
                            From = from,
                            To = to,
                            Dimension = dimension,
                            Currency = currency
                        },
                        cancellationToken)))
            .WithName("GetRevenueSummary")
            .WithSummary("What the platform earned, per currency. No cross-currency total. Staff.");
    }
}
