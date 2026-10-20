using System.Text;
using Diaspora.Modules.Financial.Application.Invoices;
using Diaspora.Modules.Financial.Application.PaymentMethods;
using Diaspora.Platform.Http;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Diaspora.Modules.Financial.Api;

/// <summary>Invoices for money the platform kept, and the (empty) register of saved payment methods.</summary>
internal static class InvoiceEndpoints
{
    /// <summary>
    /// Forbids everything but the page's own inline styles. The document loads nothing and runs
    /// nothing, so a value that escaped encoding still could not act.
    /// </summary>
    private const string DocumentPolicy =
        "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; "
        + "frame-ancestors 'none'";

    public static void Map(RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/invoices/me", async (
                [FromQuery] string? before,
                [FromQuery] int? take,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                // Number anything paid since the last read first, so the list is never behind the
                // payments. A failure here is the caller's (not signed in, not permitted) and the
                // read would fail the same way, so it is returned as is.
                var issued = await sender.Send(new IssueMyInvoicesCommand(), cancellationToken);

                if (issued.IsFailure)
                {
                    return PlatformResults.Ok(context, issued);
                }

                return PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new GetMyInvoicesQuery
                        {
                            Before = before,
                            Take = take ?? Domain.Policies.FinancialLimits.DefaultLedgerPageSize
                        },
                        cancellationToken));
            })
            .WithName("GetMyInvoices")
            .WithSummary(
                "The caller's invoices, newest number first. One per captured purchase, subscription "
                + "charge or other payment the platform kept; donations and contributions have "
                + "receipts instead. Page with ?before={number}.");

        group.MapGet("/invoices/{invoiceId:guid}", async (
                Guid invoiceId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetInvoiceQuery { InvoiceId = invoiceId }, cancellationToken)))
            .WithName("GetInvoice")
            .WithSummary("One invoice. Owner only; anybody else's is reported as not found.");

        group.MapGet("/invoices/{invoiceId:guid}/document", async (
                Guid invoiceId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
            {
                var document = await sender.Send(
                    new GetInvoiceDocumentQuery { InvoiceId = invoiceId }, cancellationToken);

                if (document.IsFailure)
                {
                    return PlatformResults.Ok(context, document);
                }

                var headers = context.Response.Headers;
                headers.ContentSecurityPolicy = DocumentPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers.CacheControl = "no-store";
                headers["Referrer-Policy"] = "no-referrer";

                return Results.Content(document.Value, "text/html; charset=utf-8", Encoding.UTF8);
            })
            .WithName("GetInvoiceDocument")
            .WithSummary(
                "The invoice as a printable HTML page. Owner only, with the same bearer token as "
                + "every other call; it is not a shareable link.")
            .Produces<string>(StatusCodes.Status200OK, "text/html");

        group.MapGet("/payment-methods/me", async (
                ISender sender, HttpContext context, CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(new GetMySavedPaymentMethodsQuery(), cancellationToken)))
            .WithName("GetMySavedPaymentMethods")
            .WithSummary(
                "Reusable payment methods the platform holds for the caller. Always empty: card "
                + "details are entered on the provider's checkout page for each payment and nothing "
                + "reusable is kept by the platform or on its behalf.");

        group.MapDelete("/payment-methods/{paymentMethodId:guid}", async (
                Guid paymentMethodId,
                ISender sender,
                HttpContext context,
                CancellationToken cancellationToken) =>
                PlatformResults.Ok(
                    context,
                    await sender.Send(
                        new ForgetSavedPaymentMethodCommand { PaymentMethodId = paymentMethodId },
                        cancellationToken)))
            .WithName("ForgetSavedPaymentMethod")
            .WithSummary(
                "Forgets a saved payment method. Not found for every identifier while none is stored.");
    }
}
