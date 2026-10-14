using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>Creates or updates a monetization product.</summary>
public sealed class UpsertMonetizationProductCommandHandler
    : ICommandHandler<UpsertMonetizationProductCommand, MonetizationProductResponse>
{
    private readonly IMonetizationProductRepository _products;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly FinancialOptions _options;

    public UpsertMonetizationProductCommandHandler(
        IMonetizationProductRepository products,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _products = products;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<MonetizationProductResponse>> Handle(
        UpsertMonetizationProductCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<MonetizationProductCategory>(command.Category, true, out var category)
            || !Enum.TryParse<MonetizationSubjectType>(command.SubjectType, true, out var subjectType))
        {
            return Result.Failure<MonetizationProductResponse>(Error.Validation(
                FinancialErrorCodes.ProductSubjectMismatch,
                "That category or subject type is not one the platform monetises."));
        }

        var now = _clock.UtcNow;
        var existing = await _products.FindByCodeAsync(command.Code, cancellationToken);
        var isNew = existing is null;

        var product = existing ?? MonetizationProduct.Define(
            MonetizationProductId.From(_guids.NewSequentialGuid()),
            command.Code,
            command.Name,
            command.Description,
            category,
            subjectType,
            now);

        product.SetPresentation(command.Name, command.Description, command.SortOrder, now);

        Money? price = null;

        if (command.PriceAmount is { } amount)
        {
            // Staff defining a catalogue price with no currency get the deployment's configured
            // default, not a literal: a deployment that is not sterling-first says so in config.
            var facts = await _currencies.FactsAsync(
                command.Currency ?? _options.DefaultCurrency, cancellationToken);

            price = Money.FromAmount(amount, facts.Code, facts.Exponent);
        }

        try
        {
            product.SetPrice(price, command.PricingPolicyKey, now);

            product.SetGrant(
                Enum.TryParse<PlacementKind>(command.PlacementKind, true, out var placementKind)
                    ? placementKind
                    : null,
                command.DurationDays,
                command.PriorityWeight ?? 1m,
                command.IsRenewable,
                now);

            product.SetAvailability(command.AvailableFrom, command.AvailableTo, now);

            if (command.Eligibility is { } rules)
            {
                product.ClearEligibility();

                foreach (var (key, value) in rules)
                {
                    if (Enum.TryParse<EligibilityRuleKind>(key, true, out var ruleKind))
                    {
                        product.AddEligibility(_guids.NewSequentialGuid(), ruleKind, value);
                    }
                }
            }

            if (command.Activate)
            {
                product.Activate(now);
            }
        }
        catch (DomainException exception)
        {
            return Result.Failure<MonetizationProductResponse>(
                Error.Validation(exception.Code, exception.Message));
        }

        if (isNew)
        {
            _products.Add(product);
        }
        else
        {
            _products.Update(product);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var symbol = product.PriceCurrency is { } currency
            ? await _currencies.SymbolAsync(currency, cancellationToken)
            : null;

        return Result.Success(FinancialMapper.ToResponse(product, symbol: symbol));
    }
}
