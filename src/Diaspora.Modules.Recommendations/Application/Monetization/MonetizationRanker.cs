using System.Globalization;
using Diaspora.Contracts.Financial.V1.Monetization;
using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Application.Monetization;

/// <summary>
/// Turns the member's own state into ranked plan and promotion suggestions.
/// <para>
/// <b>Every reason is a sentence the member can check.</b> It names their own listing, a count of
/// their own live listings against the limit their plan states, a count of promotions they bought,
/// or a profile they run — never a prediction ("would double your views") the platform cannot stand
/// behind. A signal that cannot be stated truthfully is not used.
/// </para>
/// <para>
/// <b>Never something they already hold.</b> The current plan, a plan that costs no more than it,
/// free plans and any item already promoted (running, scheduled or paused) are left out before
/// ranking, and so is anything the member dismissed.
/// </para>
/// <para>
/// Pure: no I/O, no clock of its own, so the rules can be pinned by unit tests.
/// </para>
/// </summary>
public static class MonetizationRanker
{
    public const string StrategyKey = "monetization-signals";

    /// <summary>The window "promotions you bought recently" counts over.</summary>
    public const int PurchaseLookbackDays = 60;

    /// <summary>How many recent promotion purchases make a plan with included boosts worth naming.</summary>
    public const int PromotionPurchasesForPlan = 2;

    private const string PersonalAudience = "Personal";
    private const string ListingLimitKey = "listings.concurrent";
    private const string IncludedBoostsKey = "boosts.included.monthly";
    private const string Unlimited = "unlimited";

    /// <summary>Product categories that are a promotion of something the member owns.</summary>
    private static readonly HashSet<string> NonPromotionCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreditBundle", "DonationPackage", "ContributionPackage", "SubscriptionPlan", "Campaign"
    };

    /// <summary>The owned item types a listing limit counts.</summary>
    private static readonly HashSet<string> ListingTypes = new(StringComparer.Ordinal)
    {
        nameof(RecommendationTargetType.MarketplaceListing),
        nameof(RecommendationTargetType.HousingListing)
    };

    public static MonetizationRanking Rank(MonetizationRankingInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var candidates = new List<MonetizationSuggestion>();

        candidates.AddRange(ProductSuggestions(input));
        candidates.AddRange(PlanSuggestions(input));

        var allowed = candidates
            .Where(candidate => !input.Suppressed.Contains(candidate.OfferId))
            .GroupBy(candidate => candidate.OfferId)
            .Select(group => group.OrderByDescending(candidate => candidate.Confidence).First())
            .OrderByDescending(candidate => candidate.Confidence)
            .ThenBy(candidate => candidate.SortKey, StringComparer.Ordinal)
            .ToList();

        return new MonetizationRanking(
            [.. allowed.Take(Math.Max(1, input.Limit))],
            CandidateCount: candidates.Count,
            ExcludedCount: candidates.Count - allowed.Count);
    }

    // ── Promotions for the member's own items ───────────────────────────────────────────────

    private static IEnumerable<MonetizationSuggestion> ProductSuggestions(MonetizationRankingInput input)
    {
        var snapshot = input.Snapshot;

        var held = snapshot.Holdings
            .Select(holding => (holding.SubjectType, holding.SubjectId))
            .ToHashSet();

        var boughtBefore = snapshot.Purchases
            .Select(purchase => purchase.ProductCode)
            .ToHashSet(StringComparer.Ordinal);

        // Older items first among equals: they have been live longest without help.
        var subjects = input.OwnedSubjects
            .OrderBy(subject => subject.PublishedAt ?? DateTimeOffset.MaxValue)
            .ThenBy(subject => subject.SubjectId);

        var position = 0;

        foreach (var subject in subjects)
        {
            position++;

            if (held.Contains((subject.SubjectType, subject.SubjectId)))
            {
                // Something already promotes it. A second promotion is not a recommendation anybody
                // needs, and "nothing is promoting it" would be false.
                continue;
            }

            // Keyed on the item: see MonetizationOfferKey. A dismissed item gets no product at all.
            var offerId = MonetizationOfferKey.ForSubject(subject.SubjectType, subject.SubjectId);

            var product = snapshot.Products
                .Where(product => string.Equals(product.SubjectType, subject.SubjectType, StringComparison.Ordinal)
                                  && product.EligibleSubjectIds.Contains(subject.SubjectId))
                .OrderByDescending(product => boughtBefore.Contains(product.Code))
                .ThenBy(product => product.SortOrder)
                .ThenBy(product => Price(product) ?? decimal.MaxValue)
                .ThenBy(product => product.Code, StringComparer.Ordinal)
                .FirstOrDefault();

            if (product is null)
            {
                continue;
            }

            var title = Quote(subject.Title);
            var repeat = boughtBefore.Contains(product.Code);

            var reason = repeat
                ? $"You have bought {product.Name} before, and nothing is promoting {title} right now."
                : $"Nothing is promoting {title} right now."
                  + (product.DurationDays is { } days
                      ? string.Create(CultureInfo.InvariantCulture, $" {product.Name} runs for {days} days.")
                      : string.Empty);

            yield return new MonetizationSuggestion
            {
                OfferId = offerId,
                Kind = "product",
                Code = product.Code,
                Title = $"{product.Name}: {subject.Title}",
                Reason = reason,
                ReasonCode = repeat ? "product.bought_before" : "subject.unpromoted",
                ReasonKind = repeat
                    ? RecommendationReasonCode.BoughtBefore
                    : RecommendationReasonCode.OwnsUnpromotedItem,
                Confidence = repeat ? 0.7m : 0.5m,
                Route = string.Create(
                    CultureInfo.InvariantCulture,
                    $"/monetization/promote?subjectType={Uri.EscapeDataString(subject.SubjectType)}"
                    + $"&subjectId={subject.SubjectId}&productCode={Uri.EscapeDataString(product.Code)}"),
                SubjectType = subject.SubjectType,
                SubjectId = subject.SubjectId,
                PriceAmount = Price(product),
                Currency = product.Currency,
                SortKey = string.Create(CultureInfo.InvariantCulture, $"p{position:D6}")
            };
        }
    }

    // ── Plans ───────────────────────────────────────────────────────────────────────────────

    private static IEnumerable<MonetizationSuggestion> PlanSuggestions(MonetizationRankingInput input)
    {
        var snapshot = input.Snapshot;

        // The plan the member is on. No subscription means the free personal plan, which is what
        // the platform gives everybody; its limits are the ones that apply to them.
        var current = snapshot.CurrentPlanCode is { } code
            ? snapshot.Plans.FirstOrDefault(plan => string.Equals(plan.Code, code, StringComparison.Ordinal))
            : snapshot.Plans.FirstOrDefault(plan => plan.IsFree && IsPersonal(plan));

        var currentPrice = current is null ? 0m : Price(current);

        // Plans the member could move to: paid, not the one they hold, and costing more than it.
        // Suggesting a cheaper plan with less in it is not a recommendation; it is a downgrade.
        var upgrades = snapshot.Plans
            .Where(plan => !plan.IsFree)
            .Where(plan => !string.Equals(plan.Code, snapshot.CurrentPlanCode, StringComparison.Ordinal))
            .Where(plan => current is null
                           || !string.Equals(plan.Currency, current.Currency, StringComparison.OrdinalIgnoreCase)
                           || Price(plan) > currentPrice)
            .OrderBy(plan => Price(plan))
            .ThenBy(plan => plan.SortOrder)
            .ToList();

        var route = snapshot.CurrentPlanCode is not null && current is { IsFree: false }
            ? "/monetization/change-plan?planCode="
            : "/monetization/plans?highlight=";

        foreach (var suggestion in RolePlans(input, upgrades, route))
        {
            yield return suggestion;
        }

        if (ListingLimitPlan(input, current, upgrades, route) is { } listingLimit)
        {
            yield return listingLimit;
        }

        if (IncludedBoostsPlan(input, current, upgrades, route) is { } boosts)
        {
            yield return boosts;
        }
    }

    /// <summary>A provider plan for somebody running a provider profile; an organization plan likewise.</summary>
    private static IEnumerable<MonetizationSuggestion> RolePlans(
        MonetizationRankingInput input, IReadOnlyList<MonetizationPlanOffer> upgrades, string route)
    {
        foreach (var (audience, subjectType, noun, reasonCode) in new[]
                 {
                     ("Provider", nameof(RecommendationTargetType.Provider), "service providers", "plan.matches_provider_role"),
                     ("Organization", nameof(RecommendationTargetType.Organization), "community organizations", "plan.matches_organization_role")
                 })
        {
            var owned = input.OwnedSubjects
                .Where(subject => string.Equals(subject.SubjectType, subjectType, StringComparison.Ordinal))
                .OrderBy(subject => subject.PublishedAt ?? DateTimeOffset.MaxValue)
                .FirstOrDefault();

            var plan = upgrades.FirstOrDefault(candidate =>
                string.Equals(candidate.AudienceType, audience, StringComparison.OrdinalIgnoreCase));

            if (owned is null || plan is null)
            {
                continue;
            }

            var profile = audience == "Provider" ? "provider profile" : "organization";

            yield return PlanSuggestion(
                plan,
                route,
                $"You run the {profile} {Quote(owned.Title)}, and {plan.Name} is the plan made for {noun}.",
                reasonCode,
                RecommendationReasonCode.MatchesRole,
                0.6m);
        }
    }

    /// <summary>A plan with a higher listing limit, for a member at or one short of theirs.</summary>
    private static MonetizationSuggestion? ListingLimitPlan(
        MonetizationRankingInput input,
        MonetizationPlanOffer? current,
        IReadOnlyList<MonetizationPlanOffer> upgrades,
        string route)
    {
        if (current is null || ReadLimit(current, ListingLimitKey) is not { } limit || limit <= 0)
        {
            return null;
        }

        var live = input.OwnedSubjects.Count(subject => ListingTypes.Contains(subject.SubjectType));

        if (live == 0 || live < limit - 1)
        {
            return null;
        }

        var plan = upgrades
            .Where(IsPersonal)
            .FirstOrDefault(candidate => IsUnlimited(candidate, ListingLimitKey)
                                         || ReadLimit(candidate, ListingLimitKey) > limit);

        if (plan is null)
        {
            return null;
        }

        var allows = IsUnlimited(plan, ListingLimitKey)
            ? "unlimited listings"
            : string.Create(CultureInfo.InvariantCulture, $"{ReadLimit(plan, ListingLimitKey)} at once");

        var listings = live == 1 ? "1 live listing" : string.Create(CultureInfo.InvariantCulture, $"{live} live listings");

        return PlanSuggestion(
            plan,
            route,
            string.Create(
                CultureInfo.InvariantCulture,
                $"You have {listings}, and your {current.Name} plan allows {limit} at once. {plan.Name} allows {allows}."),
            "plan.near_listing_limit",
            RecommendationReasonCode.NearPlanLimit,
            live >= limit ? 0.8m : 0.65m);
    }

    /// <summary>A plan that includes boosts, for a member who keeps buying promotions.</summary>
    private static MonetizationSuggestion? IncludedBoostsPlan(
        MonetizationRankingInput input,
        MonetizationPlanOffer? current,
        IReadOnlyList<MonetizationPlanOffer> upgrades,
        string route)
    {
        var since = input.AsOf.AddDays(-PurchaseLookbackDays);

        var bought = input.Snapshot.Purchases.Count(purchase =>
            purchase.PurchasedAt >= since && !NonPromotionCategories.Contains(purchase.Category));

        if (bought < PromotionPurchasesForPlan)
        {
            return null;
        }

        var included = current is null ? 0 : ReadLimit(current, IncludedBoostsKey) ?? 0;

        var plan = upgrades
            .Where(IsPersonal)
            .FirstOrDefault(candidate => ReadLimit(candidate, IncludedBoostsKey) is { } boosts && boosts > included);

        if (plan is null)
        {
            return null;
        }

        var monthly = ReadLimit(plan, IncludedBoostsKey)!.Value;

        return PlanSuggestion(
            plan,
            route,
            string.Create(
                CultureInfo.InvariantCulture,
                $"You bought {bought} promotions in the last {PurchaseLookbackDays} days. {plan.Name} includes "
                + $"{monthly} boost{(monthly == 1 ? string.Empty : "s")} a month."),
            "plan.includes_promotions_bought",
            RecommendationReasonCode.BuysPromotionsOften,
            0.6m);
    }

    private static MonetizationSuggestion PlanSuggestion(
        MonetizationPlanOffer plan,
        string route,
        string reason,
        string reasonCode,
        RecommendationReasonCode kind,
        decimal confidence) =>
        new()
        {
            OfferId = MonetizationOfferKey.ForPlan(plan.Code),
            Kind = "plan",
            Code = plan.Code,
            Title = $"{plan.Name} plan",
            Reason = reason,
            ReasonCode = reasonCode,
            ReasonKind = kind,
            Confidence = confidence,
            Route = route + Uri.EscapeDataString(plan.Code),
            PriceAmount = Price(plan),
            Currency = plan.Currency,
            SortKey = string.Create(CultureInfo.InvariantCulture, $"a{plan.SortOrder:D6}")
        };

    /// <summary>Major units, for comparing and for the response. The contract carries minor units.</summary>
    private static decimal Price(MonetizationPlanOffer plan) => ToMajor(plan.PriceMinorUnits, plan.PriceExponent);

    private static decimal? Price(MonetizationProductOffer product) =>
        product.PriceMinorUnits is { } minor ? ToMajor(minor, product.PriceExponent) : null;

    private static decimal ToMajor(long minorUnits, int exponent)
    {
        var scale = 1m;

        for (var place = 0; place < Math.Clamp(exponent, 0, 8); place++)
        {
            scale *= 10m;
        }

        return minorUnits / scale;
    }

    private static bool IsPersonal(MonetizationPlanOffer plan) =>
        string.Equals(plan.AudienceType, PersonalAudience, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnlimited(MonetizationPlanOffer plan, string key) =>
        plan.Entitlements.TryGetValue(key, out var value)
        && string.Equals(value, Unlimited, StringComparison.OrdinalIgnoreCase);

    private static int? ReadLimit(MonetizationPlanOffer plan, string key) =>
        plan.Entitlements.TryGetValue(key, out var value)
        && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static string Quote(string title) => $"“{title.Trim()}”";
}

/// <summary>Everything one ranking pass reads.</summary>
public sealed record MonetizationRankingInput
{
    public required MonetizationOfferSnapshot Snapshot { get; init; }

    /// <summary>The member's own live items, by Financial subject type name.</summary>
    public required IReadOnlyList<OwnedSubject> OwnedSubjects { get; init; }

    /// <summary>Offer identifiers the member dismissed or blocked.</summary>
    public required IReadOnlySet<Guid> Suppressed { get; init; }

    public required DateTimeOffset AsOf { get; init; }

    public int Limit { get; init; } = 10;
}

public sealed record OwnedSubject(
    string SubjectType, Guid SubjectId, string Title, DateTimeOffset? PublishedAt);

public sealed record MonetizationRanking(
    IReadOnlyList<MonetizationSuggestion> Items, int CandidateCount, int ExcludedCount);

public sealed record MonetizationSuggestion
{
    public required Guid OfferId { get; init; }

    /// <summary><c>product</c> or <c>plan</c>.</summary>
    public required string Kind { get; init; }

    public required string Code { get; init; }

    public required string Title { get; init; }

    public required string Reason { get; init; }

    /// <summary>Machine-readable, for analytics and the client: <c>subject.unpromoted</c>, <c>plan.near_listing_limit</c>…</summary>
    public required string ReasonCode { get; init; }

    public required RecommendationReasonCode ReasonKind { get; init; }

    public required decimal Confidence { get; init; }

    public required string Route { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public decimal? PriceAmount { get; init; }

    public string? Currency { get; init; }

    /// <summary>Tie-break among equal confidence: plans by sort order, then items oldest first.</summary>
    public required string SortKey { get; init; }
}
