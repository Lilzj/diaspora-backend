using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Promotions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Promotions;

/// <summary>
/// Answers what a code would do, and why not when it would do nothing.
/// <para>
/// <b>The refusal is the feature.</b> Expired, exhausted, wrong country, wrong product, below the
/// minimum spend, already used — every one of those is a different message on the client and a
/// different action for the member, and a single "invalid code" would be the cheapest possible
/// contract and the most expensive support queue.
/// </para>
/// </summary>
public sealed class RedeemPromotionCommandHandler
    : ICommandHandler<RedeemPromotionCommand, PromotionQuoteResponse>
{
    private readonly IPromotionRepository _promotions;
    private readonly IPromotionRedemptionRepository _redemptions;
    private readonly IMonetizationProductRepository _products;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly FinancialIdentityService _identities;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RedeemPromotionCommandHandler(
        IPromotionRepository promotions,
        IPromotionRedemptionRepository redemptions,
        IMonetizationProductRepository products,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        FinancialIdentityService identities,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _promotions = promotions;
        _redemptions = redemptions;
        _products = products;
        _authorization = authorization;
        _currencies = currencies;
        _identities = identities;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PromotionQuoteResponse>> Handle(
        RedeemPromotionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PromotionQuoteResponse>(caller.Error);
        }

        var promotion = await _promotions.FindByCodeAsync(command.Code, cancellationToken);

        if (promotion is null)
        {
            return Result.Failure<PromotionQuoteResponse>(Error.NotFound(
                FinancialErrorCodes.PromotionNotFound, "That code does not exist."));
        }

        var product = command.ProductCode is { } code
            ? await _products.FindByCodeAsync(code, cancellationToken)
            : null;

        // With no product the quote is against the code's own minimum spend, or against nothing —
        // which still answers every refusal but the product one.
        var listPrice = product?.Price
            ?? Money.Zero(promotion.Currency ?? _identities.DefaultCurrency);

        var used = await _redemptions.CountForSubjectAsync(
            promotion.Id, caller.Value, cancellationToken);

        var now = _clock.UtcNow;
        var refusal = promotion.Evaluate(
            listPrice, product?.Code ?? command.ProductCode, command.CountryCode, used, now);

        var symbol = await _currencies.SymbolAsync(listPrice.Currency, cancellationToken);

        if (refusal is not null)
        {
            return Result.Success(new PromotionQuoteResponse
            {
                Code = promotion.Code,
                IsApplicable = false,
                ListPrice = FinancialMapper.ToResponse(listPrice, symbol),
                Discount = FinancialMapper.ToResponse(
                    Money.Zero(listPrice.Currency, listPrice.Exponent), symbol),
                Payable = FinancialMapper.ToResponse(listPrice, symbol),
                RefusalCode = refusal
            });
        }

        var discount = promotion.Discount.AmountOff(listPrice);

        // Quoting writes nothing. A code is spent by the purchase that uses it, so that a member who
        // checks a coupon and then changes their mind has not lost it.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new PromotionQuoteResponse
        {
            Code = promotion.Code,
            IsApplicable = true,
            ListPrice = FinancialMapper.ToResponse(listPrice, symbol),
            Discount = FinancialMapper.ToResponse(discount, symbol),
            Payable = FinancialMapper.ToResponse(listPrice.Subtract(discount), symbol),
            RefusalCode = null
        });
    }
}
