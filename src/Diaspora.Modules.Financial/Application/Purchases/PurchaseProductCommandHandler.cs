using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.Placements;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Promotions;
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Eligibility;
using Diaspora.Modules.Financial.Extensibility.Pricing;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Purchases;

/// <summary>
/// The purchase flow, in one place.
/// <para>
/// Resolve the product, price it (from the row or from a policy), apply a coupon if one was given,
/// check eligibility, record the purchase, create the placement it grants, and open a payment intent
/// for what is owed. <b>Nothing is granted here</b> — the placement is created scheduled and
/// activates when the payment captures, because a placement activated on creation is a placement
/// somebody gets for free when the payment later fails.
/// </para>
/// <para>
/// <b>A free purchase completes immediately and opens no payment.</b> A hundred-percent coupon and a
/// zero-priced product are the same case, and forcing a zero-amount payment intent through a gateway
/// would fail at the first adapter that refuses one.
/// </para>
/// </summary>
public sealed class PurchaseProductCommandHandler
    : ICommandHandler<PurchaseProductCommand, MonetizationPurchaseResponse>
{
    private readonly IMonetizationProductRepository _products;
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionRedemptionRepository _redemptions;
    private readonly IPlacementRepository _placements;
    private readonly IPaymentIntentRepository _intents;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IFinancialPolicyEngine _pricing;
    private readonly IProductEligibilityEvaluator _eligibility;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PurchaseProductCommandHandler(
        IMonetizationProductRepository products,
        IMonetizationPurchaseRepository purchases,
        IPromotionRepository promotions,
        IPromotionRedemptionRepository redemptions,
        IPlacementRepository placements,
        IPaymentIntentRepository intents,
        ISubscriptionRepository subscriptions,
        IFinancialPolicyEngine pricing,
        IProductEligibilityEvaluator eligibility,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _purchases = purchases;
        _promotions = promotions;
        _redemptions = redemptions;
        _placements = placements;
        _intents = intents;
        _subscriptions = subscriptions;
        _pricing = pricing;
        _eligibility = eligibility;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<MonetizationPurchaseResponse>> Handle(
        PurchaseProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<MonetizationPurchaseResponse>(caller.Error);
        }

        var now = _clock.UtcNow;

        var product = await _products.FindByCodeAsync(command.ProductCode, cancellationToken);

        if (product is null)
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.NotFound(
                FinancialErrorCodes.ProductNotFound, "There is no such product."));
        }

        if (!product.IsOnSale(now))
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.Conflict(
                FinancialErrorCodes.ProductRetired, "That product is not on sale."));
        }

        var subject = ResolveSubject(command, product, caller.Value);

        if (subject.SubjectType != product.SubjectType)
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.Validation(
                FinancialErrorCodes.ProductSubjectMismatch,
                $"'{product.Code}' is bought for a {product.SubjectType}, not a {subject.SubjectType}."));
        }

        var eligible = await CheckEligibilityAsync(product, subject, caller.Value, command, now, cancellationToken);

        if (eligible.IsFailure)
        {
            return Result.Failure<MonetizationPurchaseResponse>(eligible.Error);
        }

        var priced = await PriceAsync(product, command, caller.Value, now, cancellationToken);

        if (priced.IsFailure)
        {
            return Result.Failure<MonetizationPurchaseResponse>(priced.Error);
        }

        var listPrice = priced.Value.Amount;
        var discount = Money.Zero(listPrice.Currency, listPrice.Exponent);
        Promotion? promotion = null;

        if (!string.IsNullOrWhiteSpace(command.PromotionCode))
        {
            var applied = await ApplyPromotionAsync(
                command.PromotionCode, listPrice, product, caller.Value, command.CountryCode, now,
                cancellationToken);

            if (applied.IsFailure)
            {
                return Result.Failure<MonetizationPurchaseResponse>(applied.Error);
            }

            promotion = applied.Value.Promotion;
            discount = applied.Value.Discount;
        }

        var payable = listPrice.Subtract(discount);

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            listPrice.Currency,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        var purchase = MonetizationPurchase.Record(
            MonetizationPurchaseId.From(_guids.NewSequentialGuid()),
            caller.Value,
            account.Id,
            product.Id,
            product.Code,
            product.Category,
            subject,
            listPrice,
            payable,
            now,
            priced.Value.PolicyVersion,
            command.CountryCode);

        _purchases.Add(purchase);

        if (promotion is not null)
        {
            RecordRedemption(promotion, purchase, discount, command.CountryCode, caller.Value, now);
        }

        var placement = CreatePlacement(product, purchase, subject, command, caller.Value, now, priced.Value.PolicyVersion);

        PaymentIntent? payment = null;

        if (payable.IsZero)
        {
            // Free is a real outcome: a hundred-percent coupon, or a zero-priced product. It
            // completes here rather than being pushed through a gateway that would refuse it.
            purchase.Complete(now);
            placement?.Activate(now);
        }
        else
        {
            payment = OpenPayment(purchase, payable, subject, command, account.Id, caller.Value, product, now);
            _intents.Add(payment);
            purchase.AttachPayment(payment.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(listPrice.Currency, cancellationToken);

        return Result.Success(
            FinancialMapper.ToResponse(purchase, placement, payment, now, symbol));
    }

    private async Task<Result> CheckEligibilityAsync(
        MonetizationProduct product,
        SubjectReference subject,
        Guid buyerUserId,
        PurchaseProductCommand command,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.FindActiveForSubscriberAsync(
            buyerUserId, cancellationToken);

        var hasPurchased = product.Rule(EligibilityRuleKind.OneTimeOnly) is not null
            && await _purchases.HasCompletedPurchaseAsync(
                buyerUserId, product.Code, subject, cancellationToken);

        var decision = _eligibility.Evaluate(new EligibilityContext
        {
            Product = product,
            BuyerUserId = buyerUserId,
            Subject = subject,
            AccessTier = _authorization.Claim("access_tier"),
            IsVerified = _authorization.Has(FinancialPermissions.PurchasesCreate),
            CountryCode = command.CountryCode,
            SubscriptionPlanCode = subscription?.PlanCode,
            HasPurchasedBefore = hasPurchased,
            AsOf = now
        });

        return decision.IsEligible
            ? Result.Success()
            : Result.Failure(Error.Forbidden(
                decision.ReasonCode ?? FinancialErrorCodes.ProductNotEligible,
                decision.Reason ?? "You cannot buy this."));
    }

    /// <summary>
    /// What the product costs this buyer, from the row or from a policy.
    /// <para>
    /// A product carrying its own price answers immediately; one naming a pricing policy asks the
    /// engine, which resolves the most specific active version for the country and stamps the version
    /// onto the purchase. That is what makes country overrides and campaign pricing configuration
    /// rather than code.
    /// </para>
    /// </summary>
    private async Task<Result<PricingDecision>> PriceAsync(
        MonetizationProduct product,
        PurchaseProductCommand command,
        Guid buyerUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (product.Price is { } listed && product.PricingPolicyKey is null)
        {
            return Result.Success(PricingDecision.Of(listed, null, "The catalogue price."));
        }

        var basis = product.Price ?? Money.Zero(_identities.DefaultCurrency);

        return await _pricing.PriceAsync(
            new PricingContext
            {
                PolicyKind = FinancialPolicyKind.ProductPricing,
                PolicyKey = product.PricingPolicyKey,
                BaseAmount = basis,
                ProductCode = product.Code,
                CountryCode = command.CountryCode,
                SubjectType = product.SubjectType,
                BuyerUserId = buyerUserId,
                AsOf = now
            },
            cancellationToken);
    }

    private async Task<Result<AppliedPromotion>> ApplyPromotionAsync(
        string code,
        Money listPrice,
        MonetizationProduct product,
        Guid buyerUserId,
        string? countryCode,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var promotion = await _promotions.FindByCodeAsync(code, cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<AppliedPromotion>(Error.NotFound(
                FinancialErrorCodes.PromotionNotFound, "That code does not exist."));
        }

        var used = await _redemptions.CountForSubjectAsync(
            promotion.Id, buyerUserId, cancellationToken);

        var refusal = promotion.Evaluate(listPrice, product.Code, countryCode, used, now);

        if (refusal is not null)
        {
            return Result.Failure<AppliedPromotion>(Error.Conflict(
                refusal, "That code cannot be used on this purchase."));
        }

        return Result.Success(
            new AppliedPromotion(promotion, promotion.Discount.AmountOff(listPrice)));
    }

    private void RecordRedemption(
        Promotion promotion,
        MonetizationPurchase purchase,
        Money discount,
        string? countryCode,
        Guid buyerUserId,
        DateTimeOffset now)
    {
        var redemption = PromotionRedemption.Record(
            PromotionRedemptionId.From(_guids.NewSequentialGuid()),
            promotion.Id,
            promotion.Code,
            buyerUserId,
            discount,
            now,
            purchase.Id,
            purchase.ProductCode,
            countryCode);

        _redemptions.Add(redemption);

        promotion.RecordRedemption(now);
        _promotions.Update(promotion);

        purchase.AttachPromotion(promotion.Id, promotion.Code);
    }

    /// <summary>
    /// Creates the placement a product grants, scheduled rather than active.
    /// <para>
    /// It activates when the payment captures. A placement activated at purchase is one somebody
    /// keeps when the payment later fails.
    /// </para>
    /// </summary>
    private Placement? CreatePlacement(
        MonetizationProduct product,
        MonetizationPurchase purchase,
        SubjectReference subject,
        PurchaseProductCommand command,
        Guid buyerUserId,
        DateTimeOffset now,
        string? policyVersion)
    {
        if (product.PlacementKind is not { } kind)
        {
            return null;
        }

        var startsAt = command.StartsAt is { } requested && requested > now ? requested : now;

        var window = product.DurationDays is { } days
            ? DateWindow.ForDays(startsAt, days)
            : DateWindow.Open(startsAt);

        var placement = Placement.Purchase(
            PlacementId.From(_guids.NewSequentialGuid()),
            subject,
            kind,
            window,
            product.PriorityWeight,
            buyerUserId,
            product.Code,
            now,
            purchase.Id,
            policyVersion,
            command.CountryCode);

        _placements.Add(placement);
        purchase.AttachPlacement(placement.Id);

        return placement;
    }

    private PaymentIntent OpenPayment(
        MonetizationPurchase purchase,
        Money payable,
        SubjectReference subject,
        PurchaseProductCommand command,
        FinancialAccountId accountId,
        Guid buyerUserId,
        MonetizationProduct product,
        DateTimeOffset now)
    {
        var methodKind = Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var parsed)
            ? parsed
            : PaymentMethodKind.Card;

        return PaymentIntent.Create(
            PaymentIntentId.From(_guids.NewSequentialGuid()),
            buyerUserId,
            accountId,
            payable,
            methodKind,
            product.Name,
            now,
            subject,
            command.IdempotencyKey ?? $"purchase:{purchase.Id.Value:N}",
            command.CountryCode);
    }

    private static SubjectReference ResolveSubject(
        PurchaseProductCommand command, MonetizationProduct product, Guid buyerUserId) =>
        Enum.TryParse<MonetizationSubjectType>(command.SubjectType, true, out var subjectType)
            && command.SubjectId is { } subjectId
            ? SubjectReference.Create(subjectType, subjectId)
            : product.SubjectType == MonetizationSubjectType.Member
                ? SubjectReference.ForMember(buyerUserId)
                : SubjectReference.Create(product.SubjectType, command.SubjectId ?? Guid.Empty);

    private sealed record AppliedPromotion(Promotion Promotion, Money Discount);
}
