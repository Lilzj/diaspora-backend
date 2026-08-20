using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Giving;

/// <summary>
/// Something that can be given to.
/// <para>
/// <b>The shape comes from the client, not from this brief.</b> The React Native app already ships a
/// working donation-causes screen against <c>MOCK_CAUSES</c>, and every field here answers one it
/// renders: organiser and organiser type, urgency, supporter count, verification badge, location,
/// contact numbers, and the five support kinds. Designing a different shape and asking the client to
/// migrate would have been inventing work.
/// </para>
/// <para>
/// <b>Most giving on this platform is not money.</b> Three of the five accepted kinds are goods,
/// volunteer time and professional skills, so a cause tracks <see cref="SupporterCount"/> always and
/// <see cref="RaisedMinorUnits"/> only when somebody gave money. A cause that has raised nothing and
/// gathered forty volunteers is a success, and the model has to be able to say so.
/// </para>
/// <para>
/// <b>The beneficiary is a reference this module never resolves.</b> An organization cause names
/// B12's organization id and knows nothing else about it.
/// </para>
/// </summary>
public sealed class DonationCause : AggregateRoot<DonationCauseId>, IAuditableEntity
{
    private DonationCause(
        DonationCauseId id,
        string code,
        string title,
        string description,
        BeneficiaryType beneficiaryType,
        string organizerName,
        string currency,
        DateTimeOffset createdAt) : base(id)
    {
        Code = code;
        Title = title;
        Description = description;
        BeneficiaryType = beneficiaryType;
        OrganizerName = organizerName;
        Currency = currency;
        Urgency = ContributionUrgency.Ongoing;
        IsActive = true;
        CreatedAt = createdAt;
    }

    private DonationCause() { }

    /// <summary>The stable key a donation names. Lower case, unique.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public BeneficiaryType BeneficiaryType { get; private set; }

    /// <summary>The organization, community or member who benefits. Never resolved here.</summary>
    public Guid? BeneficiaryId { get; private set; }

    public string OrganizerName { get; private set; } = string.Empty;

    public ContributionUrgency Urgency { get; private set; }

    /// <summary>Comma-separated <c>ContributionKind</c> names. What this cause will accept.</summary>
    public string AcceptedKinds { get; private set; } = string.Empty;

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>Money given so far. Maintained; reconciled by a sweep.</summary>
    public long RaisedMinorUnits { get; private set; }

    /// <summary>What the organiser is trying to raise. Null when there is no target.</summary>
    public long? TargetMinorUnits { get; private set; }

    /// <summary>Everybody who gave anything, monetary or not. The client's <c>supportersCount</c>.</summary>
    public int SupporterCount { get; private set; }

    /// <summary>Trust's word about the organiser, projected here from a published contract.</summary>
    public bool IsVerified { get; private set; }

    public bool IsActive { get; private set; }

    public string? CountryCode { get; private set; }

    public string? LocationLabel { get; private set; }

    public string? ContactPhone { get; private set; }

    public string? ContactWhatsapp { get; private set; }

    /// <summary>A Media reference, never a URL. B5's rule, honoured by every module since.</summary>
    public string? MediaReference { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Raised => Money.FromMinorUnits(RaisedMinorUnits, Currency, Exponent);

    public Money? Target => TargetMinorUnits is { } target
        ? Money.FromMinorUnits(target, Currency, Exponent)
        : null;

    public static DonationCause Open(
        DonationCauseId id,
        string code,
        string title,
        string description,
        BeneficiaryType beneficiaryType,
        string organizerName,
        string currency,
        DateTimeOffset createdAt,
        Guid? beneficiaryId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var normalized = Money.NormalizeCurrency(currency)
            ?? throw new ArgumentException(
                $"'{currency}' is not an ISO 4217 currency code.", nameof(currency));

        return new DonationCause(
            id, code.Trim().ToLowerInvariant(), title.Trim(), description.Trim(),
            beneficiaryType,
            string.IsNullOrWhiteSpace(organizerName) ? "Diaspora" : organizerName.Trim(),
            normalized, createdAt)
        {
            BeneficiaryId = beneficiaryId,
            AcceptedKinds = ContributionKind.Monetary.ToString()
        };
    }

    public void SetAcceptedKinds(IEnumerable<ContributionKind> kinds)
    {
        AcceptedKinds = string.Join(
            ',',
            (kinds ?? [ContributionKind.Monetary])
                .Distinct()
                .Order()
                .Select(kind => kind.ToString()));

        Revision++;
    }

    public void SetPresentation(
        string? title,
        string? description,
        ContributionUrgency urgency,
        string? locationLabel,
        string? mediaReference,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            Description = description.Trim();
        }

        Urgency = urgency;
        LocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? null : locationLabel.Trim();
        MediaReference = string.IsNullOrWhiteSpace(mediaReference) ? null : mediaReference.Trim();

        Touch(now);
    }

    public void SetContact(string? phone, string? whatsapp, string? countryCode, DateTimeOffset now)
    {
        ContactPhone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        ContactWhatsapp = string.IsNullOrWhiteSpace(whatsapp) ? null : whatsapp.Trim();
        CountryCode = string.IsNullOrWhiteSpace(countryCode)
            ? null
            : countryCode.Trim().ToUpperInvariant();

        Touch(now);
    }

    public void SetTarget(Money? target, DateTimeOffset now)
    {
        if (target is not null && !string.Equals(target.Currency, Currency, StringComparison.Ordinal))
        {
            throw new DomainException(
                $"A {Currency} cause cannot have a {target.Currency} target.",
                FinancialErrorCodes.CurrencyMismatch);
        }

        TargetMinorUnits = target?.MinorUnits;
        Touch(now);
    }

    /// <summary>Projected from Trust's published verification contract. Never decided here.</summary>
    public void SetVerified(bool isVerified, DateTimeOffset now)
    {
        if (IsVerified == isVerified)
        {
            return;
        }

        IsVerified = isVerified;
        Touch(now);
    }

    public void Close(DateTimeOffset now)
    {
        IsActive = false;
        Touch(now);
    }

    public void Reopen(DateTimeOffset now)
    {
        IsActive = true;
        Touch(now);
    }

    /// <summary>Whether this cause will take a gift of this kind.</summary>
    public bool Accepts(ContributionKind kind) =>
        AcceptedKinds.Length == 0
        || AcceptedKinds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Contains(kind.ToString(), StringComparer.Ordinal);

    /// <summary>Folds a completed donation into the totals.</summary>
    public void RecordDonation(Money? amount, DateTimeOffset now)
    {
        if (amount is not null)
        {
            if (!string.Equals(amount.Currency, Currency, StringComparison.Ordinal))
            {
                throw new DomainException(
                    $"A {amount.Currency} donation cannot be totalled into a {Currency} cause.",
                    FinancialErrorCodes.CurrencyMismatch);
            }

            RaisedMinorUnits += amount.MinorUnits;
        }

        SupporterCount++;
        Touch(now);
    }

    /// <summary>Sets the totals from a reconciliation sweep. The only way they can go down.</summary>
    public void ReconcileTotals(long raisedMinorUnits, int supporterCount, DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(raisedMinorUnits);
        ArgumentOutOfRangeException.ThrowIfNegative(supporterCount);

        if (RaisedMinorUnits == raisedMinorUnits && SupporterCount == supporterCount)
        {
            return;
        }

        RaisedMinorUnits = raisedMinorUnits;
        SupporterCount = supporterCount;
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
