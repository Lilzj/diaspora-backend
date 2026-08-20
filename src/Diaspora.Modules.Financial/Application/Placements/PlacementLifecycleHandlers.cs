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
using Diaspora.Modules.Financial.Domain.Purchases;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Placements;

/// <summary>
/// Reading one placement, renewing it, pausing it, resuming it and setting auto-renew.
/// <para>
/// <b>Everything here operates on the placement that exists.</b> Renewing extends its window; pausing
/// stops its clock; resuming hands back the paused days. The alternative — cancel and re-buy — throws
/// away whatever remains of a paid window, which is why the frontend refused to offer a pause against
/// the old contract rather than misdescribe what the member's money had done.
/// </para>
/// </summary>
public sealed class PlacementLifecycleHandlers
    : ICommandHandler<RenewPlacementCommand, MonetizationPurchaseResponse>,
        ICommandHandler<PausePlacementCommand, PlacementResponse>,
        ICommandHandler<ResumePlacementCommand, PlacementResponse>,
        ICommandHandler<SetPlacementAutoRenewCommand, PlacementResponse>
{
    private readonly IPlacementRepository _placements;
    private readonly IMonetizationProductRepository _products;
    private readonly IMonetizationPurchaseRepository _purchases;
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PlacementLifecycleHandlers(
        IPlacementRepository placements,
        IMonetizationProductRepository products,
        IMonetizationPurchaseRepository purchases,
        IPaymentIntentRepository intents,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _placements = placements;
        _products = products;
        _purchases = purchases;
        _intents = intents;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Buys more time on an existing placement.
    /// <para>
    /// The purchase and the payment are the ordinary ones; what differs is that the purchase is
    /// attached to the placement as a <em>renewal</em>, so capture extends the window instead of
    /// activating a second placement.
    /// </para>
    /// </summary>
    public async Task<Result<MonetizationPurchaseResponse>> Handle(
        RenewPlacementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var owned = await MineAsync(command.PlacementId, cancellationToken);

        if (owned.IsFailure)
        {
            return Result.Failure<MonetizationPurchaseResponse>(owned.Error);
        }

        var placement = owned.Value;
        var now = _clock.UtcNow;

        if (placement.Status is PlacementStatus.Cancelled or PlacementStatus.Refunded)
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.Conflict(
                FinancialErrorCodes.PlacementNotRenewable,
                $"A {placement.Status} placement cannot be renewed. Buy a new one."));
        }

        var product = await _products.FindByCodeAsync(
            command.ProductCode ?? placement.ProductCode, cancellationToken);

        if (product is null || !product.IsOnSale(now))
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.NotFound(
                FinancialErrorCodes.ProductNotFound,
                "The product behind that placement is no longer on sale."));
        }

        if (!product.IsRenewable)
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.Conflict(
                FinancialErrorCodes.PlacementNotRenewable, "That product cannot be renewed."));
        }

        if (product.DurationDays is not { } days)
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.Conflict(
                FinancialErrorCodes.PlacementDurationInvalid,
                "An open-ended placement has nothing to extend."));
        }

        // The list price from the catalogue. The client sends a product code and never a price —
        // §30's rule, and the reason this handler does not read an amount from the request at all.
        if (product.Price is not { } price)
        {
            return Result.Failure<MonetizationPurchaseResponse>(Error.Conflict(
                FinancialErrorCodes.PolicyUnresolved,
                "That product has no price the platform can charge."));
        }

        var account = await _identities.GetOrCreateAsync(
            placement.PurchasedByUserId,
            FinancialAccountType.Personal,
            price.Currency,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        var purchase = MonetizationPurchase.Record(
            MonetizationPurchaseId.From(_guids.NewSequentialGuid()),
            placement.PurchasedByUserId,
            account.Id,
            product.Id,
            product.Code,
            product.Category,
            placement.Subject,
            price,
            price,
            now,
            placement.PolicyVersion,
            command.CountryCode);

        purchase.AttachRenewal(placement.Id, days);
        _purchases.Add(purchase);

        var methodKind = Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var parsed)
            ? parsed
            : PaymentMethodKind.Card;

        var payment = PaymentIntent.Create(
            PaymentIntentId.From(_guids.NewSequentialGuid()),
            placement.PurchasedByUserId,
            account.Id,
            price,
            methodKind,
            $"{product.Name} renewal",
            now,
            placement.Subject,
            command.IdempotencyKey ?? $"placement-renewal:{purchase.Id.Value:N}",
            command.CountryCode);

        _intents.Add(payment);
        purchase.AttachPayment(payment.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = await _currencies.SymbolAsync(price.Currency, cancellationToken);

        return Result.Success(
            FinancialMapper.ToResponse(purchase, placement, payment, now, symbol));
    }

    public async Task<Result<PlacementResponse>> Handle(
        PausePlacementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await MutateAsync(
            command.PlacementId,
            placement => placement.Pause(_clock.UtcNow),
            cancellationToken);
    }

    public async Task<Result<PlacementResponse>> Handle(
        ResumePlacementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await MutateAsync(
            command.PlacementId,
            placement => placement.Resume(_clock.UtcNow),
            cancellationToken);
    }

    public async Task<Result<PlacementResponse>> Handle(
        SetPlacementAutoRenewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return await MutateAsync(
            command.PlacementId,
            placement => placement.SetAutoRenew(command.Enabled, _clock.UtcNow),
            cancellationToken);
    }

    private async Task<Result<PlacementResponse>> MutateAsync(
        Guid placementId, Action<Placement> mutate, CancellationToken cancellationToken)
    {
        var owned = await MineAsync(placementId, cancellationToken);

        if (owned.IsFailure)
        {
            return Result.Failure<PlacementResponse>(owned.Error);
        }

        try
        {
            mutate(owned.Value);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PlacementResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _placements.Update(owned.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(owned.Value, _clock.UtcNow));
    }

    /// <summary>
    /// The caller's placement, or a 404.
    /// <para>
    /// 404 rather than 403 on somebody else's: a 403 would confirm the placement exists, and a
    /// placement identifier appears in a deep link.
    /// </para>
    /// </summary>
    private async Task<Result<Placement>> MineAsync(
        Guid placementId, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<Placement>(caller.Error);
        }

        var placement = await _placements.FindAsync(
            PlacementId.From(placementId), cancellationToken);

        return placement is null
            || (placement.PurchasedByUserId != caller.Value
                && !_authorization.Has(FinancialPermissions.CatalogManage))
            ? Result.Failure<Placement>(Error.NotFound(
                FinancialErrorCodes.PlacementNotFound, "That placement does not exist."))
            : Result.Success(placement);
    }
}

/// <summary>
/// One placement of the caller's.
/// <para>
/// A separate class from the writes above because DSP4001 refuses a query handler that saves, and
/// answering 404 rather than 403 on somebody else's is the same rule the rest of this module follows:
/// a 403 confirms the placement exists, and a placement identifier appears in a deep link.
/// </para>
/// </summary>
public sealed class GetPlacementQueryHandler : IQueryHandler<GetPlacementQuery, PlacementResponse>
{
    private readonly IPlacementRepository _placements;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetPlacementQueryHandler(
        IPlacementRepository placements,
        FinancialAuthorizationService authorization,
        IClock clock)
    {
        _placements = placements;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<PlacementResponse>> Handle(
        GetPlacementQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PlacementResponse>(caller.Error);
        }

        var placement = await _placements.FindAsync(
            PlacementId.From(query.PlacementId), cancellationToken);

        return placement is null
            || (placement.PurchasedByUserId != caller.Value
                && !_authorization.Has(FinancialPermissions.CatalogManage))
            ? Result.Failure<PlacementResponse>(Error.NotFound(
                FinancialErrorCodes.PlacementNotFound, "That placement does not exist."))
            : Result.Success(FinancialMapper.ToResponse(placement, _clock.UtcNow));
    }
}
