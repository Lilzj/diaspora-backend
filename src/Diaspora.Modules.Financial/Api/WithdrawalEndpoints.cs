using Diaspora.Modules.Financial.Application.Withdrawals;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Taking money off the platform.
/// <para>
/// <b>Every one of these refuses with <c>financial.payout.not_supported</c> until a settlement
/// provider is registered</b>, which <c>GET /financial/capabilities</c> already reports as
/// <c>payoutsEnabled</c>. That is a deployment registration in the same category as the payment
/// gateway, and an honest refusal is a great deal more useful to a client than a 404 on a route it
/// expects to exist.
/// </para>
/// <para>
/// <b>No route here accepts or returns a credential.</b> A destination is registered with a label and
/// a provider token; the account number goes to the settlement provider through its own SDK and never
/// reaches this platform.
/// </para>
/// </summary>
internal static class WithdrawalEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/wallets/me/withdrawals", async (
                CreateWithdrawalRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateWithdrawalCommand
                        {
                            Amount = request.Amount,
                            Currency = request.Currency,
                            DestinationId = request.DestinationId,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey
                        },
                        cancellationToken)))
            .WithName("CreateWithdrawal")
            .WithSummary("Requests a payout and holds the money behind it.");

        group.MapGet("/wallets/me/withdrawals", async (
                [FromQuery] string? status,
                [FromQuery] DateTimeOffset? before,
                [FromQuery] Guid[]? excludeIds,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyWithdrawalsQuery
                        {
                            Status = status,
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyWithdrawals")
            .WithSummary("The caller's payout requests. Cursor-paged.");

        group.MapGet("/wallets/me/withdrawals/limits", async (
                [FromQuery] string? currency,
                [FromQuery] string? country,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetWithdrawalLimitsQuery { Currency = currency, CountryCode = country },
                        cancellationToken)))
            .WithName("GetWithdrawalLimits")
            .WithSummary("Limits, fees, and what the caller can actually spend right now.");

        group.MapGet("/withdrawals/{withdrawalId:guid}", async (
                Guid withdrawalId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetWithdrawalQuery { WithdrawalId = withdrawalId }, cancellationToken)))
            .WithName("GetWithdrawal")
            .WithSummary("One payout request. 404 on somebody else's, never 403.");

        group.MapPost("/withdrawals/{withdrawalId:guid}/cancel", async (
                Guid withdrawalId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelWithdrawalCommand { WithdrawalId = withdrawalId },
                        cancellationToken)))
            .WithName("CancelWithdrawal")
            .WithSummary("Calls it off and releases the hold. Refused once a provider has taken it.");

        group.MapPost("/payout-destinations", async (
                RegisterPayoutDestinationRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RegisterPayoutDestinationCommand
                        {
                            Kind = request.Kind,
                            Label = request.Label,
                            Currency = request.Currency,
                            Provider = request.Provider,
                            ProviderToken = request.ProviderToken,
                            CountryCode = request.CountryCode
                        },
                        cancellationToken)))
            .WithName("RegisterPayoutDestination")
            .WithSummary("Registers somewhere money can be sent. Carries no credential.");

        group.MapGet("/payout-destinations", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyPayoutDestinationsQuery(), cancellationToken)))
            .WithName("GetMyPayoutDestinations")
            .WithSummary("The caller's destinations, as masked labels.");

        group.MapDelete("/payout-destinations/{destinationId:guid}", async (
                Guid destinationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new RemovePayoutDestinationCommand { DestinationId = destinationId },
                        cancellationToken)))
            .WithName("RemovePayoutDestination")
            .WithSummary("Stops using it. Never deletes it — old withdrawals still name it.");

        group.MapPost("/payout-destinations/{destinationId:guid}/verify", async (
                Guid destinationId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new VerifyPayoutDestinationCommand { DestinationId = destinationId },
                        cancellationToken)))
            .WithName("VerifyPayoutDestination")
            .WithSummary("Records that somebody who could check did. Staff.");
    }
}
