using System.Globalization;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Invoices;

/// <summary>
/// The document a member keeps for money the platform took from them.
/// <para>
/// <b>One invoice per captured payment, and only for money the platform kept.</b> A purchase of a
/// catalogue product, a subscription charge, a membership fee. Not a donation or a contribution —
/// those carry their own receipt reference and may belong to nobody — and not a deposit, which is
/// the member's own money moving into their own balance.
/// </para>
/// <para>
/// <b>A snapshot, not a view.</b> The description, the list price, the discount and the period are
/// copied in when the number is assigned, because the product may be renamed or repriced and the
/// placement extended afterwards, and an invoice whose lines change after it was issued is not an
/// invoice. What can change — a refund against the payment — is read from the payment at read time
/// and never written here.
/// </para>
/// <para>
/// <b>No tax is stored because none of the records it is drawn from carries any.</b> The platform's
/// purchases, subscriptions and payments record a list price, a discount and what was paid. There is
/// no tax rate, tax amount or tax jurisdiction on any of them, so an invoice that showed a tax line
/// would be inventing one. When a pricing policy starts recording tax, the columns go here.
/// </para>
/// <para>
/// <b>The number is sequential per platform per calendar year</b> — <c>DSP-2026-000123</c> — and
/// uniquely indexed on (<see cref="Year"/>, <see cref="Sequence"/>) as well as on the formatted
/// string, so two issuers racing for one number lose one at the database rather than both
/// succeeding.
/// </para>
/// </summary>
public sealed class Invoice : AggregateRoot<InvoiceId>, IAuditableEntity
{
    /// <summary>The prefix every number carries.</summary>
    public const string NumberPrefix = "DSP";

    private Invoice(
        InvoiceId id,
        int year,
        long sequence,
        Guid ownerUserId,
        PaymentIntentId paymentIntentId,
        InvoiceSourceKind sourceKind,
        string description,
        Money listPrice,
        Money discount,
        Money total,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset paidAt,
        DateTimeOffset issuedAt) : base(id)
    {
        Year = year;
        Sequence = sequence;
        Number = FormatNumber(year, sequence);
        OwnerUserId = ownerUserId;
        PaymentIntentId = paymentIntentId;
        SourceKind = sourceKind;
        Description = description;
        ListMinorUnits = listPrice.MinorUnits;
        DiscountMinorUnits = discount.MinorUnits;
        TotalMinorUnits = total.MinorUnits;
        Currency = total.Currency;
        Exponent = total.Exponent;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        PaidAt = paidAt;
        IssuedAt = issuedAt;
        CreatedAt = issuedAt;
    }

    private Invoice() { }

    /// <summary>The human-readable number. Unique across the platform.</summary>
    public string Number { get; private set; } = string.Empty;

    /// <summary>The calendar year (UTC) the number was assigned in.</summary>
    public int Year { get; private set; }

    /// <summary>The position within <see cref="Year"/>. Starts at 1.</summary>
    public long Sequence { get; private set; }

    /// <summary>Who paid. The only person who may read it.</summary>
    public Guid OwnerUserId { get; private set; }

    /// <summary>The payment it was issued for. Uniquely indexed: one invoice per payment.</summary>
    public PaymentIntentId PaymentIntentId { get; private set; } = default!;

    public InvoiceSourceKind SourceKind { get; private set; }

    /// <summary>The purchase or subscription behind it. Null for <see cref="InvoiceSourceKind.Payment"/>.</summary>
    public Guid? SourceId { get; private set; }

    /// <summary>What the line says. The product's or plan's name at the time, or the payment's description.</summary>
    public string Description { get; private set; } = string.Empty;

    public long ListMinorUnits { get; private set; }

    public long DiscountMinorUnits { get; private set; }

    /// <summary>What was taken. Always <see cref="ListMinorUnits"/> less <see cref="DiscountMinorUnits"/>.</summary>
    public long TotalMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>The code that produced the discount, when one did.</summary>
    public string? PromotionCode { get; private set; }

    /// <summary>What the payment was for. Never resolved here.</summary>
    public MonetizationSubjectType? SubjectType { get; private set; }

    public Guid? SubjectId { get; private set; }

    public DateTimeOffset PeriodStart { get; private set; }

    public DateTimeOffset PeriodEnd { get; private set; }

    /// <summary>When the payment was captured.</summary>
    public DateTimeOffset PaidAt { get; private set; }

    /// <summary>When the number was assigned.</summary>
    public DateTimeOffset IssuedAt { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money ListPrice => Money.FromMinorUnits(ListMinorUnits, Currency, Exponent);

    public Money Discount => Money.FromMinorUnits(DiscountMinorUnits, Currency, Exponent);

    public Money Total => Money.FromMinorUnits(TotalMinorUnits, Currency, Exponent);

    /// <summary><c>DSP-2026-000123</c>. Six digits, widening rather than wrapping past a million.</summary>
    public static string FormatNumber(int year, long sequence) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{NumberPrefix}-{year:D4}-{sequence:D6}");

    /// <summary>
    /// Reads a number back into its year and sequence. False for anything that is not one.
    /// </summary>
    public static bool TryParseNumber(string? number, out int year, out long sequence)
    {
        year = 0;
        sequence = 0;

        if (string.IsNullOrWhiteSpace(number))
        {
            return false;
        }

        var parts = number.Trim().Split('-');

        return parts.Length == 3
            && string.Equals(parts[0], NumberPrefix, StringComparison.OrdinalIgnoreCase)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out sequence)
            && sequence > 0;
    }

    /// <summary>
    /// Issues an invoice for one captured payment.
    /// <para>
    /// The list price, the discount and the total must agree — a total that is not the list price
    /// less the discount is an invoice whose lines do not add up, and the aggregate refuses it
    /// rather than leaving the reader to notice.
    /// </para>
    /// </summary>
    public static Invoice Issue(
        InvoiceId id,
        int year,
        long sequence,
        Guid ownerUserId,
        PaymentIntentId paymentIntentId,
        InvoiceSourceKind sourceKind,
        Guid? sourceId,
        string description,
        Money listPrice,
        Money discount,
        SubjectReference? subject,
        string? promotionCode,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset paidAt,
        DateTimeOffset issuedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(paymentIntentId);
        ArgumentNullException.ThrowIfNull(listPrice);
        ArgumentNullException.ThrowIfNull(discount);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);

        if (year is < 2000 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "Not a calendar year.");
        }

        if (!listPrice.SharesCurrencyWith(discount))
        {
            throw new DomainException(
                "A discount cannot change the currency of an invoice.",
                FinancialErrorCodes.CurrencyMismatch);
        }

        if (discount > listPrice)
        {
            throw new DomainException(
                "An invoice cannot discount more than its list price.",
                FinancialErrorCodes.AmountInvalid);
        }

        var total = listPrice.Subtract(discount);

        if (total.IsZero)
        {
            throw new DomainException(
                "Nothing was paid, so there is nothing to invoice.",
                FinancialErrorCodes.AmountInvalid);
        }

        var end = periodEnd < periodStart ? periodStart : periodEnd;

        return new Invoice(
            id, year, sequence, ownerUserId, paymentIntentId, sourceKind, description.Trim(),
            listPrice, discount, total, periodStart, end, paidAt, issuedAt)
        {
            SourceId = sourceId,
            SubjectType = subject?.SubjectType,
            SubjectId = subject?.SubjectId,
            PromotionCode = string.IsNullOrWhiteSpace(promotionCode)
                ? null
                : promotionCode.Trim().ToUpperInvariant()
        };
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
