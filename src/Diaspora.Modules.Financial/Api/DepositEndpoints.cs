using Diaspora.Modules.Financial.Application.Deposits;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// Adding money to your own balance.
/// <para>
/// <b>None of these routes takes a user identifier or a wallet identifier.</b> A deposit credits the
/// primary wallet on the account the intent is opened against, resolved from the caller — so there is
/// no field a request could point at somebody else's balance.
/// </para>
/// <para>
/// <b>The limits route exists so the client never invents a minimum.</b> A minimum, a maximum and a
/// daily cap are exactly the figures a client would otherwise hard-code, and every one of them comes
/// from a versioned policy row.
/// </para>
/// </summary>
internal static class DepositEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/wallets/me/deposits", async (
                CreateDepositRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateDepositCommand
                        {
                            Amount = request.Amount,
                            Currency = request.Currency,
                            MethodKind = request.MethodKind,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey
                        },
                        cancellationToken)))
            .WithName("CreateDeposit")
            .WithSummary(
                "Starts a top-up. The wallet is credited when the payment captures, never before.");

        group.MapGet("/wallets/me/deposits", async (
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
                        new GetMyDepositsQuery
                        {
                            Status = status,
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyDeposits")
            .WithSummary("The caller's top-ups, distinct from their payments. Cursor-paged.");

        group.MapGet("/wallets/me/deposits/limits", async (
                [FromQuery] string? currency,
                [FromQuery] string? country,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetDepositLimitsQuery { Currency = currency, CountryCode = country },
                        cancellationToken)))
            .WithName("GetDepositLimits")
            .WithSummary("The minimum, the maximum, the daily cap and what is left of it.");
    }
}
