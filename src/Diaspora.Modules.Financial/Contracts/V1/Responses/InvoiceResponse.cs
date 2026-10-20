namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One issued invoice.
/// <para>
/// <b>Tax is reported as zero with no label because no record the invoice is drawn from carries
/// tax.</b> Purchases, subscriptions and payments store a list price, a discount and what was paid —
/// never a rate or a tax amount — so <see cref="Tax"/> is zero in the invoice's currency,
/// <see cref="TaxLabel"/> is null and <see cref="TaxLines"/> is empty. Nothing here is inferred
/// from a percentage.
/// </para>
/// </summary>
public sealed record InvoiceResponse
{
    public required Guid InvoiceId { get; init; }

    /// <summary><c>DSP-2026-000123</c>. Sequential per platform per year, never per member.</summary>
    public required string Number { get; init; }

    /// <summary>
    /// <c>Paid</c>, <c>PartiallyRefunded</c> or <c>Refunded</c>, read from the payment at read time.
    /// An open string: a client must render one it does not know.
    /// </summary>
    public required string Status { get; init; }

    /// <summary><c>Purchase</c>, <c>Subscription</c> or <c>Payment</c> — the record the lines came from.</summary>
    public required string Kind { get; init; }

    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    public required IReadOnlyList<InvoiceLineResponse> Lines { get; init; }

    /// <summary>The sum of the lines.</summary>
    public required MoneyResponse Subtotal { get; init; }

    /// <summary>Zero. See the type summary.</summary>
    public required MoneyResponse Tax { get; init; }

    /// <summary>Null: no tax was recorded, so there is nothing to name.</summary>
    public string? TaxLabel { get; init; }

    /// <summary>Empty: no tax was recorded. Each entry would be one tax charged, by name.</summary>
    public required IReadOnlyList<InvoiceTaxLineResponse> TaxLines { get; init; }

    /// <summary>What was taken. Subtotal plus tax.</summary>
    public required MoneyResponse Total { get; init; }

    /// <summary>What has since been returned against the payment. Zero when nothing has.</summary>
    public required MoneyResponse Refunded { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }

    public DateTimeOffset? PaidAt { get; init; }

    /// <summary>The payment that settled it.</summary>
    public Guid? PaymentIntentId { get; init; }

    /// <summary>
    /// The printable receipt, as an API-relative path. Served as HTML to the owner only, with the
    /// same bearer token as every other call — it is not a public link.
    /// </summary>
    public required string DocumentUrl { get; init; }
}

/// <summary>One line on an invoice. A discount is a line with a negative total.</summary>
public sealed record InvoiceLineResponse
{
    public required string Description { get; init; }

    public required int Quantity { get; init; }

    public required MoneyResponse UnitPrice { get; init; }

    public required MoneyResponse Total { get; init; }

    /// <summary>What the line is for, so a client can link to the listing or plan. Null when nothing.</summary>
    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }
}

/// <summary>One tax charged on an invoice. Never produced today; see <see cref="InvoiceResponse"/>.</summary>
public sealed record InvoiceTaxLineResponse
{
    public required string Label { get; init; }

    public required MoneyResponse Amount { get; init; }
}
