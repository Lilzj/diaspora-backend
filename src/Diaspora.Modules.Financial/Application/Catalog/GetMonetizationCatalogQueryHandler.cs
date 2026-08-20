using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Eligibility;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Catalog;

/// <summary>
/// Reads the catalogue and evaluates eligibility for the caller.
/// <para>
/// <b>The same evaluator the purchase path uses.</b> A product shown as available and then refused at
/// checkout is the worst version of this feature, and it is what happens the moment two code paths
/// each decide eligibility for themselves.
/// </para>
/// </summary>
public sealed class GetMonetizationCatalogQueryHandler
    : IQueryHandler<GetMonetizationCatalogQuery, IReadOnlyList<MonetizationProductResponse>>
{
    private readonly IMonetizationProductRepository _products;
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IProductEligibilityEvaluator _eligibility;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;

    public GetMonetizationCatalogQueryHandler(
        IMonetizationProductRepository products,
        IMonetizationPurchaseRepository purchases,
        ISubscriptionRepository subscriptions,
        IProductEligibilityEvaluator eligibility,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock)
    {
        _products = products;
        _purchases = purchases;
        _subscriptions = subscriptions;
        _eligibility = eligibility;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<MonetizationProductResponse>>> Handle(
        GetMonetizationCatalogQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MonetizationProductResponse>>(caller.Error);
        }

        var subjectType = Enum.TryParse<MonetizationSubjectType>(query.SubjectType, true, out var wanted)
            ? wanted
            : (MonetizationSubjectType?)null;

        var category = Enum.TryParse<MonetizationProductCategory>(query.Category, true, out var kind)
            ? kind
            : (MonetizationProductCategory?)null;

        var products = await _products.ListAvailableAsync(subjectType, category, cancellationToken);
        var facts = await _currencies.AllAsync(cancellationToken);
        var now = _clock.UtcNow;

        var subscription = await _subscriptions.FindActiveForSubscriberAsync(
            caller.Value, cancellationToken);

        var responses = new List<MonetizationProductResponse>(products.Count);

        foreach (var product in products.Where(product => product.IsOnSale(now)))
        {
            var subject = query.SubjectId is { } subjectId
                ? SubjectReference.Create(product.SubjectType, subjectId)
                : SubjectReference.ForMember(caller.Value);

            var hasPurchased = product.Rule(EligibilityRuleKind.OneTimeOnly) is not null
                && await _purchases.HasCompletedPurchaseAsync(
                    caller.Value, product.Code, subject, cancellationToken);

            var decision = _eligibility.Evaluate(new EligibilityContext
            {
                Product = product,
                BuyerUserId = caller.Value,
                Subject = subject,
                AccessTier = _authorization.Claim("access_tier"),
                IsVerified = _authorization.Has(FinancialPermissions.PurchasesCreate),
                CountryCode = query.CountryCode,
                SubscriptionPlanCode = subscription?.PlanCode,
                HasPurchasedBefore = hasPurchased,
                AsOf = now
            });

            responses.Add(FinancialMapper.ToResponse(
                product,
                decision.IsEligible,
                decision.ReasonCode,
                decision.Reason,
                product.PriceCurrency is { } currency && facts.TryGetValue(currency, out var money)
                    ? money.Symbol
                    : null));
        }

        return Result.Success<IReadOnlyList<MonetizationProductResponse>>(responses);
    }
}
