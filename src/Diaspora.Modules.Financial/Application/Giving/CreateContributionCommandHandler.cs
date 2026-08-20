using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Receipts;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>
/// Records a contribution.
/// <para>
/// <b>The monetary and non-monetary paths are genuinely different and the handler says so.</b> Money
/// opens a payment and waits; goods, hours and skills complete on the spot and update the fund's
/// contributor count without touching its total. Collapsing the two would mean storing a zero amount
/// for four fifths of what this platform's community actually gives.
/// </para>
/// </summary>
public sealed class CreateContributionCommandHandler
    : ICommandHandler<CreateContributionCommand, ContributionResponse>
{
    private readonly ICommunityContributionRepository _contributions;
    private readonly IContributionFundRepository _funds;
    private readonly IPaymentIntentRepository _intents;
    private readonly IReceiptReferenceGenerator _receipts;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreateContributionCommandHandler(
        ICommunityContributionRepository contributions,
        IContributionFundRepository funds,
        IPaymentIntentRepository intents,
        IReceiptReferenceGenerator receipts,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _contributions = contributions;
        _funds = funds;
        _intents = intents;
        _receipts = receipts;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ContributionResponse>> Handle(
        CreateContributionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<ContributionResponse>(caller.Error);
        }

        var fund = await _funds.FindByCodeAsync(command.FundCode, cancellationToken);

        if (fund is null)
        {
            return Result.Failure<ContributionResponse>(Error.NotFound(
                FinancialErrorCodes.FundNotFound, "There is no such fund."));
        }

        if (!fund.IsActive)
        {
            return Result.Failure<ContributionResponse>(Error.Conflict(
                FinancialErrorCodes.FundInactive, "That fund is closed."));
        }

        if (!Enum.TryParse<ContributionKind>(command.Kind, true, out var kind))
        {
            return Result.Failure<ContributionResponse>(Error.Validation(
                FinancialErrorCodes.ContributionKindNotAccepted,
                $"'{command.Kind}' is not a kind of support the platform records."));
        }

        if (!fund.Accepts(kind))
        {
            return Result.Failure<ContributionResponse>(Error.Conflict(
                FinancialErrorCodes.ContributionKindNotAccepted,
                "That fund does not take this kind of support."));
        }

        var now = _clock.UtcNow;
        var receipt = _receipts.Next("CON", now);

        return kind == ContributionKind.Monetary
            ? await MonetaryAsync(command, fund, caller.Value, receipt, now, cancellationToken)
            : await PledgedAsync(command, fund, kind, caller.Value, receipt, now, cancellationToken);
    }

    private async Task<Result<ContributionResponse>> MonetaryAsync(
        CreateContributionCommand command,
        ContributionFund fund,
        Guid contributorUserId,
        string receipt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (command.Amount is not { } value || value <= 0m)
        {
            return Result.Failure<ContributionResponse>(Error.Validation(
                FinancialErrorCodes.ContributionAmountRequired,
                "A monetary contribution needs an amount."));
        }

        var currency = command.Currency ?? fund.Currency;
        var facts = await _currencies.FactsAsync(currency, cancellationToken);

        if (!string.Equals(facts.Code, fund.Currency, StringComparison.Ordinal))
        {
            return Result.Failure<ContributionResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyMismatch,
                $"That fund is denominated in {fund.Currency}. The platform never converts."));
        }

        var amount = Money.FromAmount(value, facts.Code, facts.Exponent);

        var contribution = CommunityContribution.Monetary(
            CommunityContributionId.From(_guids.NewSequentialGuid()),
            fund.Id,
            fund.Code,
            contributorUserId,
            command.IsAnonymous,
            amount,
            receipt,
            now,
            command.Message,
            command.CountryCode ?? fund.CountryCode);

        _contributions.Add(contribution);

        var account = await _identities.GetOrCreateAsync(
            contributorUserId,
            FinancialAccountType.Personal,
            facts.Code,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        var methodKind = Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var method)
            ? method
            : PaymentMethodKind.Card;

        var intent = PaymentIntent.Create(
            PaymentIntentId.From(_guids.NewSequentialGuid()),
            contributorUserId,
            account.Id,
            amount,
            methodKind,
            $"Contribution: {fund.Name}",
            now,
            null,
            command.IdempotencyKey ?? $"contribution:{contribution.Id.Value:N}",
            command.CountryCode);

        _intents.Add(intent);
        contribution.AttachPayment(intent.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(contribution, intent, facts.Symbol));
    }

    private async Task<Result<ContributionResponse>> PledgedAsync(
        CreateContributionCommand command,
        ContributionFund fund,
        ContributionKind kind,
        Guid contributorUserId,
        string receipt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (command.PledgeQuantity is not { } quantity
            || quantity <= 0m
            || string.IsNullOrWhiteSpace(command.PledgeUnit))
        {
            return Result.Failure<ContributionResponse>(Error.Validation(
                FinancialErrorCodes.ContributionPledgeRequired,
                "A contribution of goods, time or skills needs a quantity and a unit — "
                + "\"3 boxes\", \"6 hours\"."));
        }

        var contribution = CommunityContribution.Pledged(
            CommunityContributionId.From(_guids.NewSequentialGuid()),
            fund.Id,
            fund.Code,
            contributorUserId,
            command.IsAnonymous,
            kind,
            PledgeDetail.Create(quantity, command.PledgeUnit, command.PledgeDescription ?? string.Empty),
            receipt,
            now,
            command.Message,
            command.CountryCode ?? fund.CountryCode);

        // Nothing else has to happen for a pledge to count.
        contribution.Complete(now);

        _contributions.Add(contribution);

        fund.RecordContribution(null, now);
        _funds.Update(fund);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(contribution));
    }
}
