using System.Globalization;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Invoices;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Application.Invoices;

/// <summary>
/// Turns a stored invoice, and the payment behind it, into what a member reads.
/// <para>
/// <b>The status is the only thing read live.</b> Everything else was fixed when the number was
/// assigned. A refund against the payment does not rewrite the invoice; it changes its status and the
/// refunded figure, which is what a credit note would say.
/// </para>
/// </summary>
public static class InvoiceView
{
    public const string StatusPaid = "Paid";
    public const string StatusPartiallyRefunded = "PartiallyRefunded";
    public const string StatusRefunded = "Refunded";

    /// <summary>The API-relative path of the printable document.</summary>
    public static string DocumentPath(Guid invoiceId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{FinancialModuleMetadata.RoutePrefix}/invoices/{invoiceId}/document");

    public static InvoiceResponse ToResponse(Invoice invoice, PaymentIntent? payment, string? symbol)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var total = invoice.Total;
        var zero = Money.Zero(total.Currency, total.Exponent);
        var refundedMinor = Math.Min(payment?.RefundedMinorUnits ?? 0, total.MinorUnits);
        var refunded = Money.FromMinorUnits(refundedMinor, total.Currency, total.Exponent);

        return new InvoiceResponse
        {
            InvoiceId = invoice.Id.Value,
            Number = invoice.Number,
            Status = StatusOf(total.MinorUnits, refundedMinor),
            Kind = invoice.SourceKind.ToString(),
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd,
            Lines = Lines(invoice, symbol),
            Subtotal = FinancialMapper.ToResponse(total, symbol),
            Tax = FinancialMapper.ToResponse(zero, symbol),
            TaxLabel = null,
            TaxLines = [],
            Total = FinancialMapper.ToResponse(total, symbol),
            Refunded = FinancialMapper.ToResponse(refunded, symbol),
            IssuedAt = invoice.IssuedAt,
            PaidAt = invoice.PaidAt,
            PaymentIntentId = invoice.PaymentIntentId.Value,
            DocumentUrl = DocumentPath(invoice.Id.Value)
        };
    }

    private static string StatusOf(long totalMinor, long refundedMinor) =>
        refundedMinor <= 0
            ? StatusPaid
            : refundedMinor >= totalMinor
                ? StatusRefunded
                : StatusPartiallyRefunded;

    private static List<InvoiceLineResponse> Lines(Invoice invoice, string? symbol)
    {
        var lines = new List<InvoiceLineResponse>
        {
            new()
            {
                Description = invoice.Description,
                Quantity = 1,
                UnitPrice = FinancialMapper.ToResponse(invoice.ListPrice, symbol),
                Total = FinancialMapper.ToResponse(invoice.ListPrice, symbol),
                SubjectType = invoice.SubjectType?.ToString(),
                SubjectId = invoice.SubjectId
            }
        };

        if (invoice.DiscountMinorUnits > 0)
        {
            var credit = Negative(invoice.Discount, symbol);

            lines.Add(new InvoiceLineResponse
            {
                Description = invoice.PromotionCode is { } code
                    ? $"Discount (promotion {code})"
                    : "Discount",
                Quantity = 1,
                UnitPrice = credit,
                Total = credit
            });
        }

        return lines;
    }

    /// <summary>A discount line. The domain's money is never negative; a line on a document can be.</summary>
    private static MoneyResponse Negative(Money money, string? symbol)
    {
        var positive = FinancialMapper.ToResponse(money, symbol);

        return positive with
        {
            Amount = -positive.Amount,
            MinorUnits = -positive.MinorUnits
        };
    }
}
