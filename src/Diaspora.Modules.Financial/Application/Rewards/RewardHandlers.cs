using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Observability;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>
/// Prices a conversion, from policy and nowhere else.
/// <para>
/// <b>One place decides what a point is worth, and both the preview and the conversion read it.</b>
/// Two code paths computing the same rate is how a preview and a charge come to disagree — and the
/// disagreement is only ever noticed by the member it shortchanged.
/// </para>
/// </summary>
public sealed class RewardConversionPricer
{
    private readonly FinancialLimitsResolver _limits;
    private readonly IRewardConversionRepository _conversions;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly FinancialOptions _options;

    public RewardConversionPricer(
        FinancialLimitsResolver limits,
        IRewardConversionRepository conversions,
        CurrencyCatalog currencies,
        IClock clock,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _limits = limits;
        _conversions = conversions;
        _currencies = currencies;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>The terms in force for a member, right now.</summary>
    public async Task<RewardConversionTerms> TermsAsync(
        Guid memberUserId,
        string currency,
        string? countryCode,
        CancellationToken cancellationToken = default)
    {
        var facts = await _currencies.FactsAsync(currency, cancellationToken);

        var policy = await _limits.MostSpecificAsync(
            FinancialPolicyKeys.RewardConversion, countryCode, cancellationToken);

        var rate = policy?.DecimalParameter(FinancialPolicyParameters.MinorUnitsPerPoint) ?? 0m;
        var dailyMaximum = policy?.Int64Parameter(FinancialPolicyParameters.DailyMaximumPoints);

        var convertedToday = dailyMaximum is null
            ? 0
            : await _conversions.PointsConvertedSinceAsync(
                memberUserId, _clock.UtcNow.AddDays(-1), cancellationToken);

        return new RewardConversionTerms
        {
            // Two switches, and both must be on: the deployment has to have decided it converts, and
            // an operator has to have published a rate. A rate with no switch would turn conversion
            // on by seeding data; a switch with no rate would price points at nothing.
            IsEnabled = _options.EnableRewardConversion && rate > 0m,
            Currency = facts.Code,
            Exponent = facts.Exponent,
            Symbol = facts.Symbol,
            MinorUnitsPerPoint = rate,
            MinimumPoints =
                policy?.Int64Parameter(FinancialPolicyParameters.MinimumPoints) ?? 1,
            MaximumPoints = policy?.Int64Parameter(FinancialPolicyParameters.MaximumPoints),
            DailyMaximumPoints = dailyMaximum,
            DailyRemainingPoints = dailyMaximum is { } cap
                ? Math.Max(0, cap - convertedToday)
                : null,
            PolicyVersion = policy?.VersionLabel
        };
    }

    /// <summary>
    /// What the conversion would produce, and whether it is permitted.
    /// <para>
    /// Refusals are returned rather than thrown so the preview can render one — a member asked to
    /// convert two points when the minimum is five should be told that, not shown an error.
    /// </para>
    /// </summary>
    public static RewardConversionQuote Quote(
        RewardConversionTerms terms, long points, long balance)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var credit = Money.FromMinorUnits(
            (long)Math.Floor(points * terms.MinorUnitsPerPoint), terms.Currency, terms.Exponent);

        var refusal = Refusal(terms, points, balance, credit);

        return new RewardConversionQuote
        {
            Points = points,
            Credit = credit,
            IsPermitted = refusal is null,
            RefusalCode = refusal?.Code,
            RefusalMessage = refusal?.Message,
            PolicyVersion = terms.PolicyVersion,
            MinorUnitsPerPoint = terms.MinorUnitsPerPoint
        };
    }

    private static (string Code, string Message)? Refusal(
        RewardConversionTerms terms, long points, long balance, Money credit)
    {
        if (!terms.IsEnabled)
        {
            return (FinancialErrorCodes.RewardConversionNotSupported,
                "Reward points cannot be converted on this deployment.");
        }

        if (points <= 0)
        {
            return (FinancialErrorCodes.RewardBelowMinimum, "Convert at least one point.");
        }

        if (points < terms.MinimumPoints)
        {
            return (FinancialErrorCodes.RewardBelowMinimum,
                $"The smallest conversion is {terms.MinimumPoints} points.");
        }

        if (terms.MaximumPoints is { } maximum && points > maximum)
        {
            return (FinancialErrorCodes.RewardAboveMaximum,
                $"The largest single conversion is {maximum} points.");
        }

        if (terms.DailyRemainingPoints is { } remaining && points > remaining)
        {
            return (FinancialErrorCodes.RewardDailyLimitReached,
                "That would take you past what you can convert in a day.");
        }

        if (points > balance)
        {
            return (FinancialErrorCodes.RewardInsufficientPoints,
                "That is more points than you hold.");
        }

        // A rate that rounds a legitimate number of points down to nothing is a member giving points
        // away. Refusing is the only answer that does not quietly take something.
        return credit.IsZero
            ? (FinancialErrorCodes.RewardBelowMinimum,
                "That many points is not yet worth anything. Save a few more.")
            : null;
    }
}

/// <summary>The terms in force for one member, in one currency, right now.</summary>
public sealed record RewardConversionTerms
{
    public required bool IsEnabled { get; init; }

    public required string Currency { get; init; }

    public required int Exponent { get; init; }

    public string? Symbol { get; init; }

    public required decimal MinorUnitsPerPoint { get; init; }

    public required long MinimumPoints { get; init; }

    public long? MaximumPoints { get; init; }

    public long? DailyMaximumPoints { get; init; }

    public long? DailyRemainingPoints { get; init; }

    public string? PolicyVersion { get; init; }
}

/// <summary>What a conversion would produce, and whether it may happen.</summary>
public sealed record RewardConversionQuote
{
    public required long Points { get; init; }

    public required Money Credit { get; init; }

    public required bool IsPermitted { get; init; }

    public string? RefusalCode { get; init; }

    public string? RefusalMessage { get; init; }

    public string? PolicyVersion { get; init; }

    public required decimal MinorUnitsPerPoint { get; init; }
}

/// <summary>
/// Turns points into credit, once.
/// <para>
/// <b>Four barriers against converting the same points twice, and every one of them is load-bearing
/// somewhere different.</b> The idempotency key is uniquely indexed, so a retried request replays.
/// The account's balance is checked and reduced inside the aggregate, so a second conversion in the
/// same unit of work sees the lower number. The account's revision is a concurrency token, so two
/// requests racing each other lose one at commit. And the daily cap is summed from the conversions
/// table rather than from the loaded aggregate, because one instance's entry collection knows only
/// what that instance has seen.
/// </para>
/// <para>
/// <b>The credit is a real ledger posting with a real counterparty.</b> Points come out of a store
/// that is not money; the credit comes out of the platform's revenue wallet, which is where the cost
/// of a rewards programme actually lands. A conversion that credited a member from nowhere would be
/// a ledger that no longer balanced.
/// </para>
/// </summary>
public sealed class ConvertRewardPointsCommandHandler
    : ICommandHandler<ConvertRewardPointsCommand, RewardConversionResponse>
{
    private readonly IRewardAccountRepository _accounts;
    private readonly IRewardConversionRepository _conversions;
    private readonly RewardConversionPricer _pricer;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly ILedgerPostingService _ledger;
    private readonly FinancialIntegrationEventRelay _relay;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public ConvertRewardPointsCommandHandler(
        IRewardAccountRepository accounts,
        IRewardConversionRepository conversions,
        RewardConversionPricer pricer,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        ILedgerPostingService ledger,
        FinancialIntegrationEventRelay relay,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _conversions = conversions;
        _pricer = pricer;
        _identities = identities;
        _authorization = authorization;
        _ledger = ledger;
        _relay = relay;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RewardConversionResponse>> Handle(
        ConvertRewardPointsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RewardConversionResponse>(caller.Error);
        }

        var terms = await _pricer.TermsAsync(
            caller.Value, command.Currency, command.CountryCode, cancellationToken);

        if (command.IdempotencyKey is { } key
            && await _conversions.FindByIdempotencyKeyAsync(caller.Value, key, cancellationToken) is { } already)
        {
            return Result.Success(
                FinancialMapperExtensions.ToResponse(already, terms.Symbol));
        }

        var account = await _accounts.FindByMemberAsync(caller.Value, cancellationToken);

        if (account is null)
        {
            return Result.Failure<RewardConversionResponse>(Error.NotFound(
                FinancialErrorCodes.RewardAccountNotFound, "You have no reward points yet."));
        }

        var quote = RewardConversionPricer.Quote(terms, command.Points, account.Balance);

        if (!quote.IsPermitted)
        {
            FinancialTelemetry.RewardConversions.Add(
                1, new KeyValuePair<string, object?>("outcome", "refused"));

            return Result.Failure<RewardConversionResponse>(Error.Conflict(
                quote.RefusalCode ?? FinancialErrorCodes.RewardNotEligible,
                quote.RefusalMessage ?? "That conversion is not available."));
        }

        var accountRecord = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            terms.Currency,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        var wallet = await _identities.PrimaryWalletAsync(
            accountRecord.Id, terms.Currency, cancellationToken);

        if (wallet is null)
        {
            return Result.Failure<RewardConversionResponse>(Error.NotFound(
                FinancialErrorCodes.WalletNotFound,
                $"You hold no {terms.Currency} wallet for the credit to land in."));
        }

        // Funded from the platform's clearing wallet, and the choice is an accounting one worth
        // stating. Converting points creates an obligation the platform will eventually settle in
        // real money — when the member spends the credit, or withdraws it and it leaves the bank.
        // Clearing is the account whose balance is by construction the platform's net position with
        // the outside world, and the only one allowed to run negative; sourcing from it makes that
        // balance include what the platform has promised as well as what it holds. Revenue would
        // have been the intuitive choice and is wrong twice over: it is not allowed to run negative,
        // so a deployment that had taken no payments could not honour a single conversion.
        var source = await _identities.PlatformWalletAsync(
            WalletType.Clearing, terms.Currency, cancellationToken);

        if (source is null)
        {
            return Result.Failure<RewardConversionResponse>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                "The platform has no clearing wallet in this currency to fund the conversion from."));
        }

        var conversionId = RewardConversionId.From(_guids.NewSequentialGuid());
        var reference = $"reward-conversion:{conversionId.Value:N}";

        try
        {
            account.Convert(_guids.NewSequentialGuid(), command.Points, reference, _clock.UtcNow);
        }
        catch (DomainException exception)
        {
            return Result.Failure<RewardConversionResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _accounts.Update(account);

        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionType.RewardConversion,
                Amount = quote.Credit,
                Description = $"Converted {command.Points} reward points",
                Audit = AuditStamp.Create(
                    FinancialActor.Member(caller.Value),
                    FinancialSource.Api,
                    $"Reward conversion of {command.Points} points",
                    policyVersion: quote.PolicyVersion),
                Legs =
                [
                    new LedgerPostingLeg
                    {
                        WalletId = source.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = quote.Credit,
                        EntryType = LedgerEntryType.RewardConversion
                    },
                    new LedgerPostingLeg
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = quote.Credit,
                        EntryType = LedgerEntryType.RewardConversion
                    }
                ],
                InitiatedByUserId = caller.Value,
                Reference = reference,
                CountryCode = command.CountryCode
            },
            cancellationToken);

        if (posting.IsFailure)
        {
            return Result.Failure<RewardConversionResponse>(posting.Error);
        }

        var conversion = RewardConversion.Record(
            conversionId,
            caller.Value,
            command.Points,
            quote.Credit,

            // The rate as applied, stored beside the amounts. Never recomputed from a later policy.
            quote.MinorUnitsPerPoint,
            wallet.Id,
            _clock.UtcNow,
            quote.PolicyVersion,
            command.IdempotencyKey,
            posting.Value.Transaction.Id);

        _conversions.Add(conversion);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _relay.PublishAsync(conversion, cancellationToken);

        FinancialTelemetry.RewardConversions.Add(
            1, new KeyValuePair<string, object?>("outcome", "converted"));

        return Result.Success(
            FinancialMapperExtensions.ToResponse(conversion, terms.Symbol));
    }
}
