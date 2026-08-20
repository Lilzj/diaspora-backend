using Diaspora.Modules.Financial.Application.Giving;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Donations and community contributions.
/// <para>
/// <c>/donations/causes</c> is the endpoint that replaces the client's <c>MOCK_CAUSES</c>. The mock's
/// shape was the specification, so the swap is a data-source change rather than a redesign.
/// </para>
/// </summary>
internal static class GivingEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/donations/causes", async (
                [FromQuery] string? country,
                [FromQuery] string? supportKind,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetDonationCausesQuery
                        {
                            CountryCode = country,
                            SupportKind = supportKind
                        },
                        cancellationToken)))
            .WithName("GetDonationCauses")
            .WithSummary("Causes somebody can give to. Most take more than money.");

        group.MapPost("/donations", async (
                CreateDonationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateDonationCommand
                        {
                            Amount = request.Amount,
                            Currency = request.Currency,
                            CauseCode = request.CauseCode,
                            BeneficiaryType = request.BeneficiaryType,
                            BeneficiaryId = request.BeneficiaryId,
                            IsAnonymous = request.IsAnonymous,
                            Message = request.Message,
                            MethodKind = request.MethodKind,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey,
                            RecurringIntervalMonths = request.RecurringIntervalMonths
                        },
                        cancellationToken)))
            .WithName("CreateDonation")
            .WithSummary("Gives money. An anonymous donation stores no donor identifier at all.");

        group.MapGet("/giving/me", async (
                [FromQuery] DateTimeOffset? before,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyGivingQuery
                        {
                            Before = before,
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyGiving")
            .WithSummary("The caller's donations and contributions. Anonymous gifts are absent.");

        group.MapGet("/receipts/{receiptReference}", async (
                string receiptReference,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new Application.Wallets.GetReceiptQuery
                        {
                            ReceiptReference = receiptReference
                        },
                        cancellationToken)))
            .WithName("GetReceipt")
            .WithSummary(
                "A receipt by its reference alone. An anonymous gift is in nobody's history and "
                + "the reference is the only handle its giver was ever issued.");

        group.MapGet("/contributions/funds", async (
                [FromQuery] string? country,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetContributionFundsQuery { CountryCode = country },
                        cancellationToken)))
            .WithName("GetContributionFunds")
            .WithSummary("The community funds. All five kinds of support by default.");

        group.MapPost("/contributions", async (
                CreateContributionRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateContributionCommand
                        {
                            FundCode = request.FundCode,
                            Kind = request.Kind,
                            Amount = request.Amount,
                            Currency = request.Currency,
                            PledgeQuantity = request.PledgeQuantity,
                            PledgeUnit = request.PledgeUnit,
                            PledgeDescription = request.PledgeDescription,
                            IsAnonymous = request.IsAnonymous,
                            Message = request.Message,
                            MethodKind = request.MethodKind,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey
                        },
                        cancellationToken)))
            .WithName("CreateContribution")
            .WithSummary("Contributes money, goods, time or skills to a community fund.");
    }
}
