using Diaspora.Modules.Financial.Application.Payments;
using Diaspora.Modules.Financial.Contracts.V1.Requests;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>
/// The payment lifecycle.
/// <para>
/// The whole state machine is here as explicit transitions rather than a single "update payment"
/// route, because a payment that could be moved anywhere by a request body is a payment whose
/// history nobody can reconstruct.
/// </para>
/// </summary>
internal static class PaymentEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/payment-intents", async (
                CreatePaymentIntentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreatePaymentIntentCommand
                        {
                            Amount = request.Amount,
                            Currency = request.Currency,
                            MethodKind = request.MethodKind,
                            Description = request.Description,
                            SubjectType = request.SubjectType,
                            SubjectId = request.SubjectId,
                            CountryCode = request.CountryCode,
                            IdempotencyKey = request.IdempotencyKey
                        },
                        cancellationToken)))
            .WithName("CreatePaymentIntent")
            .WithSummary("Starts a payment. No instrument is sent here — the adapter collects that.");

        group.MapPost("/payment-intents/{paymentIntentId:guid}/authorize", async (
                Guid paymentIntentId,
                [FromQuery] string? clientContext,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new AuthorizePaymentCommand
                        {
                            PaymentIntentId = paymentIntentId,
                            ClientContext = clientContext
                        },
                        cancellationToken)))
            .WithName("AuthorizePayment")
            .WithSummary("Hands the payment to a gateway. May answer with a challenge.");

        group.MapPost("/payment-intents/{paymentIntentId:guid}/capture", async (
                Guid paymentIntentId,
                CapturePaymentRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CapturePaymentCommand
                        {
                            PaymentIntentId = paymentIntentId,
                            Amount = request.Amount
                        },
                        cancellationToken)))
            .WithName("CapturePayment")
            .WithSummary("Takes the money and posts it to the ledger.");

        group.MapPost("/payment-intents/{paymentIntentId:guid}/cancel", async (
                Guid paymentIntentId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CancelPaymentCommand { PaymentIntentId = paymentIntentId },
                        cancellationToken)))
            .WithName("CancelPayment")
            .WithSummary("Stops a payment that has not been captured.");

        group.MapPost("/payment-intents/{paymentIntentId:guid}/refunds", async (
                Guid paymentIntentId,
                IssueRefundRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new IssueRefundCommand
                        {
                            PaymentIntentId = paymentIntentId,
                            Amount = request.Amount,
                            Reason = request.Reason,
                            Note = request.Note
                        },
                        cancellationToken)))
            .WithName("IssueRefund")
            .WithSummary("Gives money back, in whole or in part. Staff.");

        group.MapGet("/payment-intents/{paymentIntentId:guid}", async (
                Guid paymentIntentId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetPaymentIntentQuery { PaymentIntentId = paymentIntentId },
                        cancellationToken)))
            .WithName("GetPaymentIntent")
            .WithSummary("One payment, with its attempts counted and its refunds listed.");

        group.MapGet("/payment-intents", async (
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
                        new GetPaymentHistoryQuery
                        {
                            Status = status,
                            Before = before,
                            ExcludeIds = excludeIds ?? [],
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetPaymentHistory")
            .WithSummary("The caller's payments. Cursor-paged.");

        // ── Refund requests ──────────────────────────────────────────────────────────────────
        //
        // A member may ask; only staff may decide. The refund itself still runs through the one
        // refund path this module has.

        group.MapPost("/payment-intents/{paymentIntentId:guid}/refund-requests", async (
                Guid paymentIntentId,
                CreateRefundRequestRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new CreateRefundRequestCommand
                        {
                            PaymentIntentId = paymentIntentId,
                            Amount = request.Amount,
                            Reason = request.Reason,
                            Note = request.Note
                        },
                        cancellationToken)))
            .WithName("CreateRefundRequest")
            .WithSummary("Asks for a refund. Moves no money — a staff member decides.");

        group.MapGet("/refund-requests/me", async (
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyRefundRequestsQuery
                        {
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetMyRefundRequests")
            .WithSummary("The caller's refund requests, with their decisions.");

        group.MapPost("/refund-requests/{refundRequestId:guid}/withdraw", async (
                Guid refundRequestId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new WithdrawRefundRequestCommand { RefundRequestId = refundRequestId },
                        cancellationToken)))
            .WithName("WithdrawRefundRequest")
            .WithSummary("Takes back a request before anybody has answered it.");

        group.MapGet("/operations/refund-requests", async (
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetPendingRefundRequestsQuery
                        {
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken)))
            .WithName("GetPendingRefundRequests")
            .WithSummary("Requests waiting for somebody to answer. Staff.");

        group.MapPost("/operations/refund-requests/{refundRequestId:guid}/decide", async (
                Guid refundRequestId,
                DecideRefundRequestRequest request,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new DecideRefundRequestCommand
                        {
                            RefundRequestId = refundRequestId,
                            Approve = request.Approve,
                            DecisionNote = request.DecisionNote
                        },
                        cancellationToken)))
            .WithName("DecideRefundRequest")
            .WithSummary("Approves or rejects. An approval issues the refund in the same change.");
    }
}
