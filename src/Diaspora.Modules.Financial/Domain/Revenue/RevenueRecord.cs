using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Revenue;

/// <summary>
/// One rolled-up revenue measurement over one period and one dimension.
/// <para>
/// <b>A measurement, not a document.</b> It is recomputed from the ledger by a sweep and is
/// therefore disposable: delete the whole table and the next run reproduces it. That is what makes
/// it safe to re-roll a period when a late refund lands, and it is why nothing else in this module
/// reads a revenue row to make a decision.
/// </para>
/// <para>
/// <b>Gross, fees, commission, refunds and net are stored separately.</b> Storing net alone would
/// make "how much did refunds cost us this month" unanswerable without replaying the ledger, which
/// is the query this table exists to avoid.
/// </para>
/// <para>
/// <b>One currency per row, and no totals across currencies.</b> A row for GBP and a row for NGN;
/// there is no all-currency row anywhere, because producing one needs a rate and the platform does
/// not convert. A dashboard showing two figures side by side is honest; one showing a single figure
/// would not be.
/// </para>
/// </summary>
public sealed class RevenueRecord : AggregateRoot<RevenueRecordId>, IAuditableEntity
{
    private RevenueRecord(
        RevenueRecordId id,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        RevenueDimension dimension,
        string dimensionValue,
        string currency,
        DateTimeOffset createdAt) : base(id)
    {
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Dimension = dimension;
        DimensionValue = dimensionValue;
        Currency = currency;
        CreatedAt = createdAt;
    }

    private RevenueRecord() { }

    /// <summary>Inclusive.</summary>
    public DateTimeOffset PeriodStart { get; private set; }

    /// <summary>Exclusive.</summary>
    public DateTimeOffset PeriodEnd { get; private set; }

    public RevenueDimension Dimension { get; private set; }

    /// <summary>The country code, module name, product code or category. <c>*</c> for a total.</summary>
    public string DimensionValue { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>Everything that came in before anything was taken out.</summary>
    public long GrossMinorUnits { get; private set; }

    public long CommissionMinorUnits { get; private set; }

    public long FeeMinorUnits { get; private set; }

    public long RefundMinorUnits { get; private set; }

    /// <summary>What the platform actually kept. Gross less refunds, of the platform's own share.</summary>
    public long NetMinorUnits { get; private set; }

    public int TransactionCount { get; private set; }

    public DateTimeOffset ComputedAt { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Gross => Money.FromMinorUnits(GrossMinorUnits, Currency, Exponent);

    public Money Net => Money.FromMinorUnits(NetMinorUnits, Currency, Exponent);

    public Money Commission => Money.FromMinorUnits(CommissionMinorUnits, Currency, Exponent);

    public Money Fees => Money.FromMinorUnits(FeeMinorUnits, Currency, Exponent);

    public Money Refunds => Money.FromMinorUnits(RefundMinorUnits, Currency, Exponent);

    public static RevenueRecord ForPeriod(
        RevenueRecordId id,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        RevenueDimension dimension,
        string? dimensionValue,
        string currency,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);

        var normalized = Money.NormalizeCurrency(currency)
            ?? throw new ArgumentException(
                $"'{currency}' is not an ISO 4217 currency code.", nameof(currency));

        return new RevenueRecord(
            id, periodStart, periodEnd, dimension,
            string.IsNullOrWhiteSpace(dimensionValue) ? "*" : dimensionValue.Trim().ToLowerInvariant(),
            normalized, createdAt);
    }

    /// <summary>
    /// Sets every figure at once.
    /// <para>
    /// One method rather than five, because a rollup is computed as a set and a caller able to set
    /// gross without setting net is a caller who can leave the row internally inconsistent.
    /// </para>
    /// </summary>
    public void Record(
        long grossMinorUnits,
        long commissionMinorUnits,
        long feeMinorUnits,
        long refundMinorUnits,
        int transactionCount,
        DateTimeOffset computedAt)
    {
        GrossMinorUnits = Math.Max(0, grossMinorUnits);
        CommissionMinorUnits = Math.Max(0, commissionMinorUnits);
        FeeMinorUnits = Math.Max(0, feeMinorUnits);
        RefundMinorUnits = Math.Max(0, refundMinorUnits);
        TransactionCount = Math.Max(0, transactionCount);
        NetMinorUnits = Math.Max(
            0, CommissionMinorUnits + FeeMinorUnits - RefundMinorUnits);
        ComputedAt = computedAt;
        Revision++;
        ModifiedAt = computedAt;
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
