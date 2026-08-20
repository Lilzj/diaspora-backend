using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Subscriptions;

/// <summary>
/// One recurring plan in the catalogue.
/// <para>
/// <b>A plan is a bag of entitlements with a price, and the entitlements are the part that
/// matters.</b> The client renders capabilities, never plan names — a screen that switched on
/// <c>planCode == "professional"</c> would have to ship again the day somebody inserts a plan
/// between two existing ones. Adding, repricing or retiring a plan is a catalogue change with no
/// release on either side.
/// </para>
/// <para>
/// <b>Nothing in this module switches on <see cref="Tier"/>.</b> It is a label for grouping and
/// sorting; every behavioural difference between plans is an entitlement.
/// </para>
/// </summary>
public sealed class SubscriptionPlan : AggregateRoot<SubscriptionPlanId>, IAuditableEntity
{
    private readonly List<Entitlement> _entitlements = [];

    private SubscriptionPlan(
        SubscriptionPlanId id,
        string code,
        string name,
        string description,
        SubscriptionTier tier,
        BillingInterval interval,
        Money price,
        DateTimeOffset createdAt) : base(id)
    {
        Code = code;
        Name = name;
        Description = description;
        Tier = tier;
        Interval = interval;
        PriceMinorUnits = price.MinorUnits;
        Currency = price.Currency;
        Exponent = price.Exponent;
        CreatedAt = createdAt;
    }

    private SubscriptionPlan() { }

    /// <summary>The stable key a subscription names. Lower case, unique.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public SubscriptionTier Tier { get; private set; }

    public BillingInterval Interval { get; private set; }

    public long PriceMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>Days of trial a new subscriber gets. Zero for none.</summary>
    public int TrialDays { get; private set; }

    public bool IsActive { get; private set; } = true;

    /// <summary>A plan the platform ships and a deployment may not retire. The free plan.</summary>
    public bool IsSystem { get; private set; }

    public int SortOrder { get; private set; }

    /// <summary>Restricts the plan to one country. Null means everywhere.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>Who the plan is for. A provider plan is not offered to a personal account.</summary>
    public FinancialAccountType AudienceType { get; private set; } = FinancialAccountType.Personal;

    public IReadOnlyList<Entitlement> Entitlements => _entitlements.AsReadOnly();

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Price => Money.FromMinorUnits(PriceMinorUnits, Currency, Exponent);

    public bool IsFree => PriceMinorUnits == 0;

    public static SubscriptionPlan Define(
        SubscriptionPlanId id,
        string code,
        string name,
        string description,
        SubscriptionTier tier,
        BillingInterval interval,
        Money price,
        DateTimeOffset createdAt,
        int trialDays = 0,
        bool isSystem = false,
        FinancialAccountType audienceType = FinancialAccountType.Personal,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(price);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new SubscriptionPlan(
            id, code.Trim().ToLowerInvariant(), name.Trim(), description.Trim(),
            tier, interval, price, createdAt)
        {
            TrialDays = Math.Max(0, trialDays),
            IsSystem = isSystem,
            AudienceType = audienceType,
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };
    }

    public void Grant(Guid entitlementId, string key, string? value = null)
    {
        var normalized = key?.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        _entitlements.RemoveAll(entitlement =>
            string.Equals(entitlement.Key, normalized, StringComparison.Ordinal));

        _entitlements.Add(Entitlement.Create(entitlementId, normalized, value));
        Revision++;
    }

    public void Reprice(Money price, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(price);

        PriceMinorUnits = price.MinorUnits;
        Currency = price.Currency;
        Exponent = price.Exponent;
        Touch(now);
    }

    public void SetPresentation(string name, string description, int sortOrder, DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            Description = description.Trim();
        }

        SortOrder = sortOrder;
        Touch(now);
    }

    public void Retire(DateTimeOffset now)
    {
        if (IsSystem)
        {
            throw new DomainException(
                $"'{Code}' is a platform plan and cannot be retired.",
                FinancialErrorCodes.PolicyImmutable);
        }

        IsActive = false;
        Touch(now);
    }

    public void Reinstate(DateTimeOffset now)
    {
        IsActive = true;
        Touch(now);
    }

    /// <summary>The capability keys this plan grants, for a published contract.</summary>
    public IReadOnlyList<string> EntitlementKeys =>
        [.. _entitlements.Select(entitlement => entitlement.Key).Order(StringComparer.Ordinal)];

    /// <summary>When a period starting now would end. Null for a lifetime plan.</summary>
    public DateTimeOffset? NextPeriodEnd(DateTimeOffset from) => Interval switch
    {
        BillingInterval.Monthly => from.AddMonths(1),
        BillingInterval.Quarterly => from.AddMonths(3),
        BillingInterval.Yearly => from.AddYears(1),
        _ => null
    };

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
