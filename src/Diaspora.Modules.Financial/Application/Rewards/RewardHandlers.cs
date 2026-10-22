using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.EventHandlers;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Accounts;
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
using Microsoft.EntityFrameworkCore;
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
    private readonly IFinancialAccountRepository _accounts;
    private readonly MemberCurrency _memberCurrency;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly FinancialOptions _options;

    public RewardConversionPricer(
        FinancialLimitsResolver limits,
        IRewardConversionRepository conversions,
        IFinancialAccountRepository accounts,
        MemberCurrency memberCurrency,
        CurrencyCatalog currencies,
        IClock clock,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _limits = limits;
        _conversions = conversions;
        _accounts = accounts;
        _memberCurrency = memberCurrency;
        _currencies = currencies;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>
    /// The terms in force for a member, right now.
    /// <para>
    /// <b>The server decides both the country and the currency.</b> The rate used to resolve by a
    /// country the request named and then apply to a currency the request named, while
    /// <c>minorUnitsPerPoint</c> meant nothing without a currency — a hundred minor units is a pound
    /// or a hundred kobo. Any member could pick the country with the best rate and the currency whose
    /// minor unit was worth the most. Now:
    /// </para>
    /// <list type="bullet">
    ///   <item>the country is the one on the member's own financial account, set by registration and
    ///   verification, never by a request;</item>
    ///   <item>a rate is in exactly one currency — the policy's <c>currency</c> parameter — and a
    ///   rate without a supported currency converts nothing;</item>
    ///   <item><paramref name="requestedCurrency"/> may only confirm that currency. Asking for another
    ///   is refused, not repriced.</item>
    /// </list>
    /// </summary>
    public async Task<RewardConversionTerms> TermsAsync(
        Guid memberUserId,
        string? requestedCurrency,
        CancellationToken cancellationToken = default)
    {
        var account = await _accounts.FindByOwnerAsync(
            memberUserId, FinancialAccountType.Personal, cancellationToken);

        var policy = await _limits.MostSpecificAsync(
            FinancialPolicyKeys.RewardConversion, account?.CountryCode, cancellationToken);

        var rate = policy?.DecimalParameter(FinancialPolicyParameters.MinorUnitsPerPoint) ?? 0m;
        var dailyMaximum = policy?.Int64Parameter(FinancialPolicyParameters.DailyMaximumPoints);

        var offered = CurrencyCode(policy?.Parameter(FinancialPolicyParameters.Currency));

        if (offered is not null && !await _currencies.IsSupportedAsync(offered, cancellationToken))
        {
            offered = null;
        }

        var requested = CurrencyCode(requestedCurrency);

        // What the terms are shown in when nothing converts: the member's own currency, so the
        // screen still reads naturally.
        var currency = offered
            ?? requested
            ?? await _memberCurrency.ForMemberAsync(memberUserId, cancellationToken);

        var facts = await _currencies.FactsAsync(currency, cancellationToken);

        var convertedToday = dailyMaximum is null
            ? 0
            : await _conversions.PointsConvertedSinceAsync(
                memberUserId, _clock.UtcNow.AddDays(-1), cancellationToken);

        // Three switches, and all must be on: the deployment has to have decided it converts, an
        // operator has to have published a rate, and the rate has to say what currency it is in. A
        // rate with no switch would turn conversion on by seeding data; a switch with no rate would
        // price points at nothing; a rate with no currency is a number with no unit.
        (string Code, string Message)? disabled =
            !_options.EnableRewardConversion || rate <= 0m || offered is null
                ? (FinancialErrorCodes.RewardConversionNotSupported,
                    "Reward points cannot be converted on this deployment.")
                : requested is not null && !string.Equals(requested, offered, StringComparison.Ordinal)
                    ? (FinancialErrorCodes.RewardCurrencyNotOffered,
                        $"Reward points convert into {offered} only.")
                    : null;

        // The same bar withdrawals set (WithdrawalHandlers: account.CanDebit). Converting turns points
        // into money in the withdrawable primary wallet, so it is held to the rule for moving money
        // out: an unverified (Pending) or Restricted account converts nothing.
        (string Code, string Message)? standing =
            account is null || account.Status == FinancialAccountStatus.Pending
                ? (FinancialErrorCodes.AccountNotEligible,
                    "Verify your identity before converting points into money. "
                    + "Your points are safe in the meantime.")
                : !account.CanDebit
                    ? (FinancialErrorCodes.AccountRestricted,
                        account.RestrictionReason ?? "This account cannot convert points right now.")
                    : null;

        return new RewardConversionTerms
        {
            IsEnabled = disabled is null,
            DisabledCode = disabled?.Code,
            AccountRefusalCode = standing?.Code,
            AccountRefusalMessage = standing?.Message,
            DisabledMessage = disabled?.Message,
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

    private static string? CurrencyCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    /// <summary>
    /// What the conversion would produce, and whether it is permitted.
    /// <para>
    /// Refusals are returned rather than thrown so the preview can render one — a member asked to
    /// convert two points when the minimum is five should be told that, not shown an error.
    /// </para>
    /// </summary>
    /// <para>
    /// <b>Only the points that buy whole minor units leave the balance.</b> The credit is floored —
    /// a fraction of a minor unit cannot be paid — and this used to debit every point asked for
    /// anyway: at 0.01 minor units a point, 550 points paid 5 and took 550, so 50 points bought
    /// nothing and vanished. <see cref="RewardConversionQuote.PointsConsumed"/> is the fewest points
    /// that buy exactly the same credit, and the rest stay with the member.
    /// </para>
    /// <para>
    /// Why it is never more than was asked for, and never changes the credit: with
    /// <c>c = floor(p·r)</c>, <c>p·r ≥ c</c>, so <c>p ≥ c/r</c> and <c>p ≥ ceil(c/r)</c>; and
    /// <c>ceil(c/r)·r ≥ c</c> while <c>ceil(c/r)·r ≤ p·r &lt; c + 1</c>, so the consumed points floor
    /// to exactly <c>c</c>. All of it in <see cref="decimal"/> and <see cref="long"/>, never
    /// floating point.
    /// </para>
    /// <param name="isHeld">
    /// True when the member's points account is held from conversion (a fraud or review hold). A
    /// refusal here, rather than the aggregate's exception later, so the preview can say it is held —
    /// in <see cref="HeldMessage"/>, never the hold's own reason.
    /// </param>
    public static RewardConversionQuote Quote(
        RewardConversionTerms terms, long points, long balance, bool isHeld = false)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var creditMinorUnits = points > 0 && terms.MinorUnitsPerPoint > 0m
            ? (long)Math.Floor(points * terms.MinorUnitsPerPoint)
            : 0L;

        var consumed = creditMinorUnits == 0
            ? 0L
            : (long)Math.Ceiling(creditMinorUnits / terms.MinorUnitsPerPoint);

        if (consumed > points)
        {
            // Unreachable by the argument above; refused loudly rather than debiting more than asked.
            throw new InvalidOperationException(
                $"A conversion of {points} points priced {consumed} points; the rate arithmetic is wrong.");
        }

        var credit = Money.FromMinorUnits(creditMinorUnits, terms.Currency, terms.Exponent);

        var refusal = Refusal(terms, points, consumed, balance, credit, isHeld);

        return new RewardConversionQuote
        {
            Points = points,
            PointsConsumed = consumed,
            Credit = credit,
            IsPermitted = refusal is null,
            RefusalCode = refusal?.Code,
            RefusalMessage = refusal?.Message,
            PolicyVersion = terms.PolicyVersion,
            MinorUnitsPerPoint = terms.MinorUnitsPerPoint
        };
    }

    /// <summary>
    /// Why not, if not. The limits read <paramref name="consumed"/> — what would actually leave the
    /// balance — while "more than you hold" reads what was asked for, which is the number the
    /// member typed.
    /// </summary>
    private static (string Code, string Message)? Refusal(
        RewardConversionTerms terms,
        long points,
        long consumed,
        long balance,
        Money credit,
        bool isHeld)
    {
        if (Gate(terms, isHeld) is { } gate)
        {
            return gate;
        }

        if (points <= 0)
        {
            return (FinancialErrorCodes.RewardBelowMinimum, "Convert at least one point.");
        }

        if (points > balance)
        {
            return (FinancialErrorCodes.RewardInsufficientPoints,
                "That is more points than you hold.");
        }

        // A rate that rounds a legitimate number of points down to nothing is a member giving points
        // away. Refusing is the only answer that does not quietly take something.
        if (credit.IsZero)
        {
            return (FinancialErrorCodes.RewardBelowMinimum,
                "That many points is not yet worth anything. Save a few more.");
        }

        if (consumed < terms.MinimumPoints)
        {
            return (FinancialErrorCodes.RewardBelowMinimum,
                $"The smallest conversion is {terms.MinimumPoints} points.");
        }

        if (terms.MaximumPoints is { } maximum && consumed > maximum)
        {
            return (FinancialErrorCodes.RewardAboveMaximum,
                $"The largest single conversion is {maximum} points.");
        }

        if (terms.DailyRemainingPoints is { } remaining && consumed > remaining)
        {
            return (FinancialErrorCodes.RewardDailyLimitReached,
                "That would take you past what you can convert in a day.");
        }

        return null;
    }

    /// <summary>
    /// What a member is told when their points account is held from conversion.
    /// <para>
    /// <b>Never the hold's own reason.</b> <see cref="RewardAccount.BlockReason"/> is written by
    /// whoever placed the hold — a fraud signal, a review note — and a refusal that names the check it
    /// tripped is a map for getting round it. The reason stays on the account for staff. (A financial
    /// account's <c>RestrictionReason</c> is different: deposits, payments and withdrawals all show it,
    /// and conversion does the same.)
    /// </para>
    /// </summary>
    public const string HeldMessage =
        "Conversion is paused on your account. Contact support if you think this is a mistake.";

    /// <summary>
    /// Whether this member may convert at all right now, and if so how much — before they type a
    /// number.
    /// <para>
    /// The app used to work this out as <c>terms.IsEnabled &amp;&amp; balance.CanConvert</c>.
    /// <c>CanConvert</c> is false below the minimum and carries no reason, so a member short of the
    /// minimum was told "Conversion is not available on your account" and never saw the minimum.
    /// Checked in the order a member can do something about: the deployment, the account, a hold, the
    /// balance, then today's allowance.
    /// </para>
    /// </summary>
    public static RewardConversionStanding Standing(
        RewardConversionTerms terms, long balance, bool isHeld)
    {
        ArgumentNullException.ThrowIfNull(terms);

        var refusal = Gate(terms, isHeld);

        if (refusal is null && balance < terms.MinimumPoints)
        {
            refusal = (FinancialErrorCodes.RewardBelowMinimum,
                $"You need at least {terms.MinimumPoints} points to convert. You have {balance}.");
        }

        // Less left today than the smallest conversion is as good as none left.
        if (refusal is null
            && terms.DailyRemainingPoints is { } remaining
            && remaining < terms.MinimumPoints)
        {
            refusal = (FinancialErrorCodes.RewardDailyLimitReached,
                "You have converted as much as you can today. You can convert again tomorrow.");
        }

        return new RewardConversionStanding
        {
            CanConvert = refusal is null,
            ReasonCode = refusal?.Code,
            ReasonMessage = refusal?.Message,

            // The smallest of every limit that applies, so a client never has to take a minimum of
            // its own — the app used to fall back to the balance and label it as policy.
            MaximumNow = refusal is null
                ? Math.Min(
                    balance,
                    Math.Min(
                        terms.MaximumPoints ?? long.MaxValue,
                        terms.DailyRemainingPoints ?? long.MaxValue))
                : null
        };
    }

    /// <summary>
    /// The refusals that do not depend on how many points: the deployment, the member's financial
    /// account, and a hold. One place, so the terms and every quote check them in the same order.
    /// </summary>
    private static (string Code, string Message)? Gate(RewardConversionTerms terms, bool isHeld)
    {
        if (!terms.IsEnabled)
        {
            return (terms.DisabledCode ?? FinancialErrorCodes.RewardConversionNotSupported,
                terms.DisabledMessage ?? "Reward points cannot be converted on this deployment.");
        }

        if (terms.AccountRefusalCode is { } accountCode)
        {
            return (accountCode, terms.AccountRefusalMessage ?? "This account cannot convert points right now.");
        }

        if (isHeld)
        {
            return (FinancialErrorCodes.RewardNotEligible, HeldMessage);
        }

        return null;
    }
}

/// <summary>
/// Whether a member may convert right now, why not, and the most they may convert. See
/// <see cref="RewardConversionPricer.Standing"/>.
/// </summary>
public sealed record RewardConversionStanding
{
    public required bool CanConvert { get; init; }

    public string? ReasonCode { get; init; }

    public string? ReasonMessage { get; init; }

    /// <summary>The smallest of the single, daily and balance limits. Null when it may not convert.</summary>
    public long? MaximumNow { get; init; }
}

/// <summary>The terms in force for one member, in one currency, right now.</summary>
public sealed record RewardConversionTerms
{
    public required bool IsEnabled { get; init; }

    /// <summary>Why not, as a code. Null when enabled.</summary>
    public string? DisabledCode { get; init; }

    /// <summary>Why not, as a sentence. Null when enabled.</summary>
    public string? DisabledMessage { get; init; }

    /// <summary>
    /// Why this member's account may not convert, as a code — <c>financial.account.not_eligible</c>
    /// or <c>financial.account.restricted</c>. Null when it may.
    /// </summary>
    public string? AccountRefusalCode { get; init; }

    /// <summary>Why not, as a sentence the member reads. Null when it may.</summary>
    public string? AccountRefusalMessage { get; init; }

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
    /// <summary>What was asked for.</summary>
    public required long Points { get; init; }

    /// <summary>
    /// What would leave the balance: the fewest points that buy <see cref="Credit"/>. Never more
    /// than <see cref="Points"/>; the difference stays with the member.
    /// </summary>
    public required long PointsConsumed { get; init; }

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
/// that is not money; the credit comes out of the platform's <em>clearing</em> wallet — see the note
/// at the posting for why clearing and not revenue (this summary used to say revenue). A conversion
/// that credited a member from nowhere would be a ledger that no longer balanced.
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

        var terms = await _pricer.TermsAsync(caller.Value, command.Currency, cancellationToken);

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

        var quote = RewardConversionPricer.Quote(
            terms, command.Points, account.Balance, IsHeld(account));

        if (!quote.IsPermitted)
        {
            FinancialTelemetry.RewardConversions.Add(
                1, new KeyValuePair<string, object?>("outcome", "refused"));

            var code = quote.RefusalCode ?? FinancialErrorCodes.RewardNotEligible;
            var message = quote.RefusalMessage ?? "That conversion is not available.";

            // An account that may not move money out is a 403, as it is for a withdrawal; everything
            // else about the request (limits, balance, rate) stays a 409.
            return Result.Failure<RewardConversionResponse>(
                code is FinancialErrorCodes.AccountNotEligible or FinancialErrorCodes.AccountRestricted
                    ? Error.Forbidden(code, message)
                    : Error.Conflict(code, message));
        }

        // No country from the request: an account opened here would keep it, and the account's
        // country is what selects the conversion rate next time.
        var accountRecord = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            terms.Currency,
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
            // The consumed points, not the requested ones: the remainder bought nothing and stays.
            account.Convert(_guids.NewSequentialGuid(), quote.PointsConsumed, reference, _clock.UtcNow);
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
                Description = $"Converted {quote.PointsConsumed} reward points",
                Audit = AuditStamp.Create(
                    FinancialActor.Member(caller.Value),
                    FinancialSource.Api,
                    $"Reward conversion of {quote.PointsConsumed} points",
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
            quote.PointsConsumed,
            quote.Credit,

            // The rate as applied, stored beside the amounts. Never recomputed from a later policy.
            quote.MinorUnitsPerPoint,
            wallet.Id,
            _clock.UtcNow,
            quote.PolicyVersion,
            command.IdempotencyKey,
            posting.Value.Transaction.Id);

        _conversions.Add(conversion);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // The replay check above cannot see a request with the same key still in flight; the
        // per-member unique index is what refuses it. This command is [Transactional], so the
        // violation has aborted the transaction — on Postgres nothing more can run in it, and a
        // failed Result would be committed rather than rolled back. Rethrown as the platform's
        // concurrency conflict: the transaction rolls back, the client gets 409, and its retry with
        // the same key replays the conversion that won.
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw new ConcurrencyConflictException(
                "A conversion with this idempotency key is still being processed. Try again in a moment.",
                exception);
        }

        await _relay.PublishAsync(conversion, cancellationToken);

        FinancialTelemetry.RewardConversions.Add(
            1, new KeyValuePair<string, object?>("outcome", "converted"));

        return Result.Success(
            FinancialMapperExtensions.ToResponse(conversion, terms.Symbol) with
            {
                PointsRequested = command.Points
            });
    }

    /// <summary>Whether the points account is held from conversion.</summary>
    internal static bool IsHeld(RewardAccount? account) => account is { IsConversionBlocked: true };

    /// <summary>
    /// Whether a failed save was a unique-index violation. Matched on the message, as the Creator and
    /// Programme modules do: production runs Npgsql and the suite SQLite, and naming either driver's
    /// exception here would put a database provider in the application layer.
    /// </summary>
    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var message = current.Message;

            if (message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key value", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
