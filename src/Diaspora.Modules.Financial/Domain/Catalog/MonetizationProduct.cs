using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Catalog;

/// <summary>
/// One thing that can be bought here, as reference data.
/// <para>
/// <b>There is no hard-coded premium feature anywhere on this platform, and this row is why.</b>
/// "Boosted job", "featured provider", "premium marketplace listing" are catalogue entries with a
/// price, a duration, an eligibility set and a placement kind — not code paths. Adding a product,
/// repricing one, restricting one to two countries or retiring one is an operator action with an
/// audit trail; none of them is a deployment, and nothing in this module switches on a product code.
/// </para>
/// <para>
/// <b>The price can live here or in a policy, and both are supported on purpose.</b> Most products
/// have one price and carry it on the row. A product whose price varies by country, by campaign or
/// by season names a <see cref="PricingPolicyKey"/> instead and the policy engine answers — which is
/// what makes "future pricing rules" real rather than decorative.
/// </para>
/// <para>
/// <b><see cref="SubjectType"/> is what the product is bought <em>for</em>.</b> A boost for a
/// marketplace listing and a boost for a job are two products, because their prices, durations and
/// audiences differ; they share a category and a placement kind, and nothing else.
/// </para>
/// </summary>
public sealed class MonetizationProduct : AggregateRoot<MonetizationProductId>, IAuditableEntity
{
    private readonly List<EligibilityRule> _eligibility = [];

    private MonetizationProduct(
        MonetizationProductId id,
        string code,
        string name,
        string description,
        MonetizationProductCategory category,
        MonetizationSubjectType subjectType,
        DateTimeOffset createdAt) : base(id)
    {
        Code = code;
        Name = name;
        Description = description;
        Category = category;
        SubjectType = subjectType;
        Status = MonetizationProductStatus.Draft;
        PriorityWeight = 1m;
        CreatedAt = createdAt;
    }

    private MonetizationProduct() { }

    /// <summary>The stable key a purchase names. Lower case, unique across the platform.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public MonetizationProductCategory Category { get; private set; }

    public MonetizationSubjectType SubjectType { get; private set; }

    public MonetizationProductStatus Status { get; private set; }

    /// <summary>The list price in minor units. Null when a pricing policy decides instead.</summary>
    public long? PriceMinorUnits { get; private set; }

    public string? PriceCurrency { get; private set; }

    public int? PriceExponent { get; private set; }

    /// <summary>
    /// The list price, rebuilt on read. Null when <see cref="PricingPolicyKey"/> decides instead.
    /// <para>
    /// Three nullable columns rather than an optional owned type. EF maps an optional owned
    /// reference by making every column nullable and then cannot distinguish "no price" from "a
    /// price whose properties are all null" — and "no price" is a real, common answer here.
    /// </para>
    /// </summary>
    public Money? Price =>
        PriceMinorUnits is { } minorUnits && PriceCurrency is { } currency
            ? Money.FromMinorUnits(minorUnits, currency, PriceExponent ?? Money.DefaultExponent)
            : null;

    /// <summary>A policy key that answers the price. Null when the row carries it.</summary>
    public string? PricingPolicyKey { get; private set; }

    /// <summary>How long what is bought lasts. Null for a one-off with no duration.</summary>
    public int? DurationDays { get; private set; }

    /// <summary>What placement this grants, when it grants one. Null for a credit bundle.</summary>
    public PlacementKind? PlacementKind { get; private set; }

    /// <summary>
    /// What a ranking consumer should weight the subject by while the placement runs. 1.0 is no
    /// change. Published in the placement contract; B14 decides what to do with it, and this module
    /// deliberately does not know.
    /// </summary>
    public decimal PriorityWeight { get; private set; }

    /// <summary>Whether buying again extends rather than replaces.</summary>
    public bool IsRenewable { get; private set; }

    /// <summary>Lower sorts first in the catalogue the client renders.</summary>
    public int SortOrder { get; private set; }

    /// <summary>A product the platform ships and a deployment may not delete.</summary>
    public bool IsSystem { get; private set; }

    /// <summary>When the product starts being offered. Null means immediately.</summary>
    public DateTimeOffset? AvailableFrom { get; private set; }

    /// <summary>When it stops being offered. Exclusive. Null means never.</summary>
    public DateTimeOffset? AvailableTo { get; private set; }

    public IReadOnlyList<EligibilityRule> Eligibility => _eligibility.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static MonetizationProduct Define(
        MonetizationProductId id,
        string code,
        string name,
        string description,
        MonetizationProductCategory category,
        MonetizationSubjectType subjectType,
        DateTimeOffset createdAt,
        bool isSystem = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var product = new MonetizationProduct(
            id, code.Trim().ToLowerInvariant(), name.Trim(), description.Trim(),
            category, subjectType, createdAt)
        {
            IsSystem = isSystem
        };

        product.Announce("created", createdAt);

        return product;
    }

    public void SetPrice(Money? price, string? pricingPolicyKey, DateTimeOffset now)
    {
        if (price is null && string.IsNullOrWhiteSpace(pricingPolicyKey))
        {
            throw new DomainException(
                "A product needs either a price or a pricing policy. A product with neither cannot "
                + "be bought and cannot say why.",
                FinancialErrorCodes.ProductNotEligible);
        }

        PriceMinorUnits = price?.MinorUnits;
        PriceCurrency = price?.Currency;
        PriceExponent = price?.Exponent;
        PricingPolicyKey = string.IsNullOrWhiteSpace(pricingPolicyKey)
            ? null
            : pricingPolicyKey.Trim().ToLowerInvariant();

        Touch(now);
        Announce("repriced", now);
    }

    public void SetGrant(
        PlacementKind? placementKind,
        int? durationDays,
        decimal priorityWeight,
        bool isRenewable,
        DateTimeOffset now)
    {
        if (durationDays is { } days && days is <= 0 or > Policies.FinancialLimits.MaxPlacementDays)
        {
            throw new DomainException(
                $"A duration must be between 1 and {Policies.FinancialLimits.MaxPlacementDays} days.",
                FinancialErrorCodes.PlacementDurationInvalid);
        }

        PlacementKind = placementKind;
        DurationDays = durationDays;
        PriorityWeight = Math.Clamp(priorityWeight, 0.1m, 10m);
        IsRenewable = isRenewable;

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
        Announce("updated", now);
    }

    public void SetAvailability(DateTimeOffset? from, DateTimeOffset? to, DateTimeOffset now)
    {
        if (from is { } start && to is { } end && end <= start)
        {
            throw new DomainException(
                "A product's availability must end after it starts.",
                FinancialErrorCodes.ProductNotEligible);
        }

        AvailableFrom = from;
        AvailableTo = to;
        Touch(now);
    }

    public void AddEligibility(Guid ruleId, EligibilityRuleKind kind, string? value)
    {
        _eligibility.RemoveAll(rule => rule.Kind == kind);
        _eligibility.Add(EligibilityRule.Create(ruleId, kind, value));
        Revision++;
    }

    public void ClearEligibility()
    {
        _eligibility.Clear();
        Revision++;
    }

    public void Activate(DateTimeOffset now)
    {
        if (Status == MonetizationProductStatus.Active)
        {
            return;
        }

        if (PriceMinorUnits is null && PricingPolicyKey is null)
        {
            throw new DomainException(
                "A product with no price and no pricing policy cannot be activated.",
                FinancialErrorCodes.ProductNotEligible);
        }

        Status = MonetizationProductStatus.Active;
        Touch(now);
        Announce("updated", now);
    }

    /// <summary>
    /// Withdraws the product from sale.
    /// <para>
    /// Existing purchases are untouched and still honoured. Retiring a product must never revoke
    /// what somebody already paid for — that is a refund decision, and it belongs to a person rather
    /// than to a catalogue edit.
    /// </para>
    /// </summary>
    public void Retire(DateTimeOffset now)
    {
        if (IsSystem)
        {
            throw new DomainException(
                $"'{Code}' is a platform product and cannot be retired.",
                FinancialErrorCodes.PolicyImmutable);
        }

        if (Status == MonetizationProductStatus.Retired)
        {
            return;
        }

        Status = MonetizationProductStatus.Retired;
        Touch(now);
        Announce("retired", now);
    }

    /// <summary>Whether the product is on sale at a moment.</summary>
    public bool IsOnSale(DateTimeOffset moment) =>
        Status == MonetizationProductStatus.Active
        && (AvailableFrom is not { } from || moment >= from)
        && (AvailableTo is not { } to || moment < to);

    public EligibilityRule? Rule(EligibilityRuleKind kind) =>
        _eligibility.Find(rule => rule.Kind == kind);

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

    private void Announce(string changeType, DateTimeOffset changedAt) =>
        RaiseDomainEvent(new MonetizationCatalogChangedDomainEvent
        {
            ProductId = Id,
            ProductCode = Code,
            Category = Category,
            Status = Status,
            ChangeType = changeType,
            ChangedAt = changedAt
        });

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
