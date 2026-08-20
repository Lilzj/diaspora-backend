using Diaspora.Modules.Financial.Application.Giving;
using Diaspora.Modules.Financial.Application.Wallets;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// The member's own money.
/// <para>
/// None of these routes takes a user identifier. A financial endpoint that accepted one is a
/// financial endpoint somebody would eventually pass somebody else's — the statement route takes a
/// wallet id and the handler checks ownership, answering 404 rather than 403 so the identifier
/// cannot be used to discover whether a wallet exists.
/// </para>
/// </summary>
internal static class WalletEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/wallets/me", async (
                [FromQuery] string? currency,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMyWalletQuery { Currency = currency }, cancellationToken)))
            .WithName("GetMyWallets")
            .WithSummary("Every wallet the caller holds, with all five balances.");

        group.MapGet("/wallets/{walletId:guid}/ledger", async (
                Guid walletId,
                [FromQuery] long? afterSequence,
                [FromQuery] int? pageSize,
                [FromQuery] string? entryType,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetLedgerQuery
                        {
                            WalletId = walletId,
                            AfterSequence = afterSequence,
                            PageSize = pageSize
                                ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize,
                            EntryType = entryType
                        },
                        cancellationToken)))
            .WithName("GetWalletLedger")
            .WithSummary("A page of a wallet's statement, newest first. Paged by sequence.");

        // The query has supported From, To, Before and ExcludeIds since B15 — a full keyset cursor
        // under ADR-017 — and the route bound none of them, so a client could fetch one page and had
        // no way to ask for the next. Binding them is the whole of that fix.
        group.MapGet("/wallets/me/transactions", async (
                [FromQuery] string? type,
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                [FromQuery] DateTimeOffset? before,
                [FromQuery] Guid[]? excludeIds,
                [FromQuery] int? pageSize,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetTransactionsQuery
                        {
                            TransactionType = type,
                            From = from,
                            To = to,
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            PageSize = pageSize
                                ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyTransactions")
            .WithSummary(
                "The caller's transaction history, filtered and cursor-paged. Exclude by identity "
                + "at the boundary instant rather than by a strict timestamp — see ADR-017.");

        group.MapGet("/transactions/{transactionId:guid}", async (
                Guid transactionId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetTransactionQuery { TransactionId = transactionId },
                        cancellationToken)))
            .WithName("GetTransaction")
            .WithSummary(
                "One transaction with its ledger entries inline. A member sees their own legs; "
                + "staff see the platform's too.");

        group.MapGet("/wallets/me/analytics", async (
                [FromQuery] DateTimeOffset? from,
                [FromQuery] DateTimeOffset? to,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetWalletAnalyticsQuery { From = from, To = to }, cancellationToken)))
            .WithName("GetMyWalletAnalytics")
            .WithSummary(
                "Money in and out over a period, summed server-side and grouped by currency. "
                + "Never totalled across currencies — the platform does not convert.");

        group.MapPost("/wallets", async (
                CreateWalletRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ProvisionWalletCommand
                        {
                            OwnerId = request.OwnerId,
                            AccountType = request.AccountType,
                            Currency = request.Currency,
                            DisplayName = request.DisplayName,
                            CountryCode = request.CountryCode
                        },
                        cancellationToken)))
            .WithName("ProvisionWallet")
            .WithSummary("Opens a financial identity and its wallets. Staff.");

        group.MapPut("/wallets/{walletId:guid}/status", async (
                Guid walletId,
                UpdateWalletStatusRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new UpdateWalletStatusCommand
                        {
                            WalletId = walletId,
                            Status = request.Status,
                            Reason = request.Reason
                        },
                        cancellationToken)))
            .WithName("UpdateWalletStatus")
            .WithSummary("Freezes, unfreezes or closes a wallet. Staff.");
    }
}
