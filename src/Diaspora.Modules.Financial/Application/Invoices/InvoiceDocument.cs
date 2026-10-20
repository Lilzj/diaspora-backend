using System.Globalization;
using System.Net;
using System.Text;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Invoices;

/// <summary>
/// Renders an invoice as a self-contained, printable HTML page.
/// <para>
/// <b>Every value is HTML-encoded.</b> A direct payment's description is whatever the payer typed, and
/// this page is served from the API's own origin; an unencoded description would be script running
/// with the API's cookies. The page also loads nothing — no script, no font, no image — and the
/// route sends a content security policy that forbids it, so a missed encoding still runs nothing.
/// </para>
/// </summary>
public static class InvoiceDocument
{
    public static string Render(InvoiceResponse invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var rows = new StringBuilder();

        foreach (var line in invoice.Lines)
        {
            rows.Append(CultureInfo.InvariantCulture, $"""
                <tr><td>{E(line.Description)}</td><td class="n">{line.Quantity}</td><td class="n">{Amount(line.UnitPrice)}</td><td class="n">{Amount(line.Total)}</td></tr>

                """);
        }

        var refunded = invoice.Refunded.MinorUnits > 0
            ? $"""<tr><th>Refunded</th><td class="n">{Amount(invoice.Refunded)}</td></tr>"""
            : string.Empty;

        var payment = invoice.PaymentIntentId is { } paymentId
            ? $"<p class=\"meta\">Payment reference {E(paymentId.ToString("D"))}</p>"
            : string.Empty;

        var number = E(invoice.Number);

        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <meta name="referrer" content="no-referrer">
            <meta name="robots" content="noindex">
            <title>Invoice {{number}}</title>
            <style>
              body { font-family: system-ui, -apple-system, "Segoe UI", sans-serif; margin: 0;
                     background: #fff; color: #14161a; padding: 24px; }
              main { max-width: 40rem; margin: 0 auto; }
              h1 { font-size: 1.5rem; margin: 0 0 .25rem; }
              .meta { color: #55606e; margin: 0 0 .25rem; }
              table { width: 100%; border-collapse: collapse; margin: 1.5rem 0; }
              th, td { text-align: left; padding: .5rem; border-bottom: 1px solid #e3e6ea; }
              .n { text-align: right; white-space: nowrap; }
              .totals th { text-align: right; font-weight: 600; }
              .status { display: inline-block; padding: .125rem .5rem; border-radius: 999px;
                        background: #eef2f6; font-size: .875rem; }
            </style>
            </head>
            <body>
            <main>
              <h1>Invoice {{number}}</h1>
              <p class="meta">Diaspora</p>
              <p class="meta">Issued {{Date(invoice.IssuedAt)}}{{(invoice.PaidAt is { } paid ? " · Paid " + Date(paid) : string.Empty)}}</p>
              <p class="meta">Period {{Date(invoice.PeriodStart)}} – {{Date(invoice.PeriodEnd)}}</p>
              <p><span class="status">{{E(invoice.Status)}}</span></p>
              <table>
                <thead><tr><th>Description</th><th class="n">Qty</th><th class="n">Unit price</th><th class="n">Amount</th></tr></thead>
                <tbody>
            {{rows}}    </tbody>
              </table>
              <table class="totals">
                <tr><th>Subtotal</th><td class="n">{{Amount(invoice.Subtotal)}}</td></tr>
                <tr><th>Tax</th><td class="n">{{Amount(invoice.Tax)}}</td></tr>
                <tr><th>Total</th><td class="n">{{Amount(invoice.Total)}}</td></tr>
                {{refunded}}
              </table>
              <p class="meta">No tax was recorded on this payment.</p>
              {{payment}}
            </main>
            </body>
            </html>
            """;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Date(DateTimeOffset value) =>
        E(value.UtcDateTime.ToString("d MMMM yyyy", CultureInfo.InvariantCulture));

    /// <summary>The currency's own number of decimals, its symbol when the catalogue has one, else its code.</summary>
    private static string Amount(MoneyResponse money)
    {
        var figure = money.Amount.ToString(
            "N" + money.Exponent.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        var text = string.IsNullOrWhiteSpace(money.Symbol)
            ? $"{money.Currency} {figure}"
            : figure.StartsWith('-')
                ? $"-{money.Symbol}{figure[1..]}"
                : $"{money.Symbol}{figure}";

        return E(text);
    }
}
