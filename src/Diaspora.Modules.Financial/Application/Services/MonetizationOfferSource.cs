using Diaspora.Contracts.Financial.V1.Monetization;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Eligibility;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Financial's half of the monetization recommendations seam: the catalogue as this member may buy
/// it, and what they already hold.
/// <para>
/// <b>Eligibility is the purchase route's own evaluator</b>, run per subject with the same context
/// <c>GET /financial/catalog</c> builds — so a recommended product is one the purchase would accept,
/// and a product the rules refuse is never offered to the recommender at all.
/// </para>
/// <para>
/// <b>Held means running, scheduled or paused, by anybody.</b> A placement awaiting its payment is
/// scheduled, so a promotion the member has just started paying for is not suggested again.
/// </para>
/// </summary>
public sealed class MonetizationOfferSource : IMonetizationOfferSource
{
    /// <summary>How many completed purchases are read back. Enough for "bought before" and a 60-day count.</summary>
    private const int PurchaseWindow = 100;

    /// <summary>How many of the member's own placements are read back to find the held ones.</summary>
    private const int PlacementWindow = 200;

    private static readonly PlacementStatus[] HeldStatuses =
        [PlacementStatus.Scheduled, PlacementStatus.Active, PlacementStatus.Paused];

    private readonly IMonetizationProductRepository _products;
    private readonly ISubscriptionPlanRepository _plans;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly IPlacementRepository _placements;
    private readonly IProductEligibilityEvaluator _eligibility;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;

    public MonetizationOfferSource(
        IMonetizationProductRepository products,
        ISubscriptionPlanRepository plans,
        ISubscriptionRepository subscriptions,
        IMonetizationPurchaseRepository purchases,
        IPlacementRepository placements,
        IProductEligibilityEvaluator eligibility,
        FinancialAuthorizationService authorization,
        IClock clock)
    {
        _products = products;
        _plans = plans;
        _subscriptions = subscriptions;
        _purchases = purchases;
        _placements = placements;
        _eligibility = eligibility;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<MonetizationOfferSnapshot> ReadAsync(
        MonetizationOfferQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var userId = query.UserId;

        var subjects = query.Subjects
            .Select(subject => Enum.TryParse<MonetizationSubjectType>(subject.SubjectType, true, out var type)
                && type != MonetizationSubjectType.Member
                    ? SubjectReference.Create(type, subject.SubjectId)
                    : null)
            .OfType<SubjectReference>()
            .DistinctBy(subject => (subject.SubjectType, subject.SubjectId))
            .ToList();

        var subscription = await _subscriptions.FindActiveForSubscriberAsync(userId, cancellationToken);

        var completed = await _purchases.ListByBuyerAsync(
            userId, PurchaseStatus.Completed, null, [], PurchaseWindow, cancellationToken);

        var products = await _products.ListAvailableAsync(null, null, cancellationToken);
        var offers = new List<MonetizationProductOffer>();

        foreach (var product in products.Where(product => product.IsOnSale(now)))
        {
            var eligible = new List<Guid>();

            foreach (var subject in subjects.Where(subject => subject.SubjectType == product.SubjectType))
            {
                if (Evaluate(product, subject, userId, query.CountryCode, subscription?.PlanCode, completed, now))
                {
                    eligible.Add(subject.SubjectId);
                }
            }

            var forMember = product.SubjectType == MonetizationSubjectType.Member
                && Evaluate(
                    product,
                    SubjectReference.ForMember(userId),
                    userId,
                    query.CountryCode,
                    subscription?.PlanCode,
                    completed,
                    now);

            offers.Add(new MonetizationProductOffer
            {
                ProductId = product.Id.Value,
                Code = product.Code,
                Name = product.Name,
                Description = product.Description,
                Category = product.Category.ToString(),
                SubjectType = product.SubjectType.ToString(),
                PriceMinorUnits = product.Price?.MinorUnits,
                PriceExponent = product.Price?.Exponent ?? 2,
                Currency = product.Price?.Currency,
                DurationDays = product.DurationDays,
                SortOrder = product.SortOrder,
                EligibleSubjectIds = eligible,
                IsEligibleForMember = forMember
            });
        }

        var plans = await _plans.ListActiveAsync(null, query.CountryCode, cancellationToken);

        return new MonetizationOfferSnapshot
        {
            Products = offers,
            Plans =
            [
                .. plans.Select(plan => new MonetizationPlanOffer
                {
                    PlanId = plan.Id.Value,
                    Code = plan.Code,
                    Name = plan.Name,
                    Description = plan.Description,
                    Tier = plan.Tier.ToString(),
                    Interval = plan.Interval.ToString(),
                    PriceMinorUnits = plan.PriceMinorUnits,
                    PriceExponent = plan.Exponent,
                    Currency = plan.Currency,
                    AudienceType = plan.AudienceType.ToString(),
                    SortOrder = plan.SortOrder,
                    TrialDays = plan.TrialDays,
                    IsFree = plan.IsFree,
                    Entitlements = plan.Entitlements
                        .GroupBy(entitlement => entitlement.Key, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.First().Value, StringComparer.Ordinal)
                })
            ],
            CurrentPlanCode = subscription?.PlanCode,
            Holdings = await HoldingsAsync(userId, subjects, now, cancellationToken),
            Purchases =
            [
                .. completed
                    .Select(purchase => new MonetizationPastPurchase(
                        purchase.ProductCode,
                        purchase.Category.ToString(),
                        purchase.Subject.SubjectType.ToString(),
                        purchase.Subject.SubjectId,
                        purchase.CompletedAt ?? purchase.CreatedAt))
                    .OrderByDescending(purchase => purchase.PurchasedAt)
            ]
        };
    }

    private bool Evaluate(
        MonetizationProduct product,
        SubjectReference subject,
        Guid userId,
        string? countryCode,
        string? planCode,
        IReadOnlyList<MonetizationPurchase> completed,
        DateTimeOffset now)
    {
        // The catalogue route asks the database for this; the completed purchases are already in
        // hand, and they answer the same question for the one rule that asks it.
        var hasPurchased = product.Rule(EligibilityRuleKind.OneTimeOnly) is not null
            && completed.Any(purchase =>
                string.Equals(purchase.ProductCode, product.Code, StringComparison.Ordinal)
                && purchase.Subject.SubjectType == subject.SubjectType
                && purchase.Subject.SubjectId == subject.SubjectId);

        return _eligibility.Evaluate(new EligibilityContext
        {
            Product = product,
            BuyerUserId = userId,
            Subject = subject,
            AccessTier = _authorization.Claim("access_tier"),
            IsVerified = _authorization.Has(FinancialPermissions.PurchasesCreate),
            CountryCode = countryCode,
            SubscriptionPlanCode = planCode,
            HasPurchasedBefore = hasPurchased,
            AsOf = now
        }).IsEligible;
    }

    private async Task<IReadOnlyList<MonetizationHolding>> HoldingsAsync(
        Guid userId,
        IReadOnlyList<SubjectReference> subjects,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var held = new Dictionary<Guid, Placement>();

        // Scheduled and paused placements are the member's own — a purchase awaiting its payment —
        // so they are read by purchaser.
        var own = await _placements.ListByPurchaserAsync(
            userId, null, null, [], PlacementWindow, cancellationToken);

        foreach (var placement in own.Where(placement =>
                     HeldStatuses.Contains(placement.Status)
                     && (placement.Window.EndsAt is null || placement.Window.EndsAt > now)))
        {
            held[placement.Id.Value] = placement;
        }

        // Running placements on the member's subjects, whoever bought them.
        foreach (var group in subjects.GroupBy(subject => subject.SubjectType))
        {
            var running = await _placements.ListActiveForSubjectsAsync(
                group.Key, [.. group.Select(subject => subject.SubjectId)], now, cancellationToken);

            foreach (var placement in running)
            {
                held[placement.Id.Value] = placement;
            }
        }

        return
        [
            .. held.Values.Select(placement => new MonetizationHolding(
                placement.ProductCode,
                placement.Subject.SubjectType.ToString(),
                placement.Subject.SubjectId,
                placement.Status.ToString(),
                placement.Window.EndsAt))
        ];
    }
}
