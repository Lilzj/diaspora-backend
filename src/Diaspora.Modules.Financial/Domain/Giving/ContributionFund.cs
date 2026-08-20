using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>
/// A standing fund contributions accumulate into.
/// <para>
/// The brief's four: a community fund, diaspora assistance, emergency relief and community projects,
/// plus country-specific funds through <see cref="CountryCode"/>. Reference data seeded at startup
/// and extended by an operator, never by a release.
/// </para>
/// <para>
/// <b>A fund is not a wallet.</b> It is the thing people give to; the money lives in the wallet
/// belonging to the fund's financial account, derived from the ledger like every other balance.
/// Keeping a running total here as well is a projection for the screen, reconciled by a sweep — the
/// same maintained-counter trade B6 recorded, made explicit rather than discovered.
/// </para>
/// </summary>
public sealed class ContributionFund : AggregateRoot<ContributionFundId>, IAuditableEntity
{
    private ContributionFund(
        ContributionFundId id,
        string code,
        string name,
        string description,
        string currency,
        DateTimeOffset createdAt) : base(id)
    {
        Code = code;
        Name = name;
        Description = description;
        Currency = currency;
        Urgency = ContributionUrgency.Ongoing;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private ContributionFund() { }

    /// <summary>The stable key a contribution names. Lower case, unique.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public ContributionUrgency Urgency { get; private set; }

    /// <summary>Restricts the fund to one country. Null means platform-wide.</summary>
    public string? CountryCode { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>Comma-separated <c>ContributionKind</c> names.</summary>
    public string AcceptedKinds { get; private set; } = string.Empty;

    public long RaisedMinorUnits { get; private set; }

    public long? TargetMinorUnits { get; private set; }

    public int ContributorCount { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>A fund the platform ships and a deployment may not delete.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>The fund's own financial account. Where contributed money actually lands.</summary>
    public FinancialAccountId? AccountId { get; private set; }

    public string? MediaReference { get; private set; }

    public int SortOrder { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Raised => Money.FromMinorUnits(RaisedMinorUnits, Currency, Exponent);

    public Money? Target => TargetMinorUnits is { } target
        ? Money.FromMinorUnits(target, Currency, Exponent)
        : null;

    public static ContributionFund Establish(
        ContributionFundId id,
        string code,
        string name,
        string description,
        string currency,
        DateTimeOffset createdAt,
        bool isSystem = false,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var normalized = Money.NormalizeCurrency(currency)
            ?? throw new ArgumentException(
                $"'{currency}' is not an ISO 4217 currency code.", nameof(currency));

        return new ContributionFund(
            id, code.Trim().ToLowerInvariant(), name.Trim(), description.Trim(), normalized, createdAt)
        {
            IsSystem = isSystem,
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant(),
            AcceptedKinds = string.Join(
                ',', Enum.GetValues<ContributionKind>().Select(kind => kind.ToString()))
        };
    }

    public void SetAcceptedKinds(IEnumerable<ContributionKind> kinds)
    {
        AcceptedKinds = string.Join(
            ',',
            (kinds ?? [ContributionKind.Monetary]).Distinct().Order().Select(kind => kind.ToString()));

        Revision++;
    }

    public void AttachAccount(FinancialAccountId accountId)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        AccountId = accountId;
        Revision++;
    }

    public void SetPresentation(
        string? name,
        string? description,
        ContributionUrgency urgency,
        int sortOrder,
        string? mediaReference,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            Description = description.Trim();
        }

        Urgency = urgency;
        SortOrder = sortOrder;
        MediaReference = string.IsNullOrWhiteSpace(mediaReference) ? null : mediaReference.Trim();

        Touch(now);
    }

    public void SetTarget(Money? target, DateTimeOffset now)
    {
        if (target is not null && !string.Equals(target.Currency, Currency, StringComparison.Ordinal))
        {
            throw new DomainException(
                $"A {Currency} fund cannot have a {target.Currency} target.",
                FinancialErrorCodes.CurrencyMismatch);
        }

        TargetMinorUnits = target?.MinorUnits;
        Touch(now);
    }

    public void Close(DateTimeOffset now)
    {
        if (IsSystem)
        {
            throw new DomainException(
                $"'{Code}' is a platform fund and cannot be closed.",
                FinancialErrorCodes.PolicyImmutable);
        }

        IsActive = false;
        Touch(now);
    }

    public bool Accepts(ContributionKind kind) =>
        AcceptedKinds.Length == 0
        || AcceptedKinds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(kind.ToString(), StringComparer.Ordinal);

    public void RecordContribution(Money? amount, DateTimeOffset now)
    {
        if (amount is not null)
        {
            if (!string.Equals(amount.Currency, Currency, StringComparison.Ordinal))
            {
                throw new DomainException(
                    $"A {amount.Currency} contribution cannot be totalled into a {Currency} fund.",
                    FinancialErrorCodes.CurrencyMismatch);
            }

            RaisedMinorUnits += amount.MinorUnits;
        }

        ContributorCount++;
        Touch(now);
    }

    public void ReconcileTotals(long raisedMinorUnits, int contributorCount, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(raisedMinorUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(contributorCount);

        if (RaisedMinorUnits == raisedMinorUnits && ContributorCount == contributorCount)
        {
            return;
        }

        RaisedMinorUnits = raisedMinorUnits;
        ContributorCount = contributorCount;
        Touch(now);
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

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
