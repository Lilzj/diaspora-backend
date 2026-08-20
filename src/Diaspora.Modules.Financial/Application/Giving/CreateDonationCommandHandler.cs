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
/// Records a donation and opens the payment for it.
/// <para>
/// <b>An anonymous donation never stores the donor's identifier</b> — not even to hide it later. The
/// receipt reference is what the donor keeps, and it is deliberately readable over a telephone
/// because it is the only key they will have.
/// </para>
/// </summary>
public sealed class CreateDonationCommandHandler
    : ICommandHandler<CreateDonationCommand, DonationResponse>
{
    private readonly IDonationRepository _donations;
    private readonly IDonationCauseRepository _causes;
    private readonly IPaymentIntentRepository _intents;
    private readonly IRecurringDonationRepository _schedules;
    private readonly IReceiptReferenceGenerator _receipts;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;
    private readonly Options.FinancialOptions _options;

    public CreateDonationCommandHandler(
        IDonationRepository donations,
        IDonationCauseRepository causes,
        IPaymentIntentRepository intents,
        IRecurringDonationRepository schedules,
        IReceiptReferenceGenerator receipts,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork,
        Microsoft.Extensions.Options.IOptions<Options.FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _donations = donations;
        _causes = causes;
        _intents = intents;
        _schedules = schedules;
        _receipts = receipts;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
        _options = options.Value;
    }

    public async Task<Result<DonationResponse>> Handle(
        CreateDonationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<DonationResponse>(caller.Error);
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        if (!await _currencies.IsSupportedAsync(command.Currency, cancellationToken))
        {
            return Result.Failure<DonationResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{command.Currency}' is not a currency this deployment takes."));
        }

        var replayed = await ReplayAsync(command, caller.Value, facts.Symbol, cancellationToken);

        if (replayed is not null)
        {
            return replayed.Value;
        }

        DonationCause? cause = null;

        if (!string.IsNullOrWhiteSpace(command.CauseCode))
        {
            cause = await _causes.FindByCodeAsync(command.CauseCode, cancellationToken);

            if (cause is null)
            {
                return Result.Failure<DonationResponse>(Error.NotFound(
                    FinancialErrorCodes.CauseNotFound, "There is no such cause."));
            }

            if (!cause.IsActive)
            {
                return Result.Failure<DonationResponse>(Error.Conflict(
                    FinancialErrorCodes.CauseInactive, "That cause is no longer taking support."));
            }

            if (!cause.Accepts(ContributionKind.Monetary))
            {
                return Result.Failure<DonationResponse>(Error.Conflict(
                    FinancialErrorCodes.ContributionKindNotAccepted,
                    "That cause does not take money. See what it does take."));
            }

            if (!string.Equals(cause.Currency, facts.Code, StringComparison.Ordinal))
            {
                return Result.Failure<DonationResponse>(Error.Validation(
                    FinancialErrorCodes.CurrencyMismatch,
                    $"That cause is denominated in {cause.Currency}. The platform never converts."));
            }
        }

        var beneficiaryType = Enum.TryParse<BeneficiaryType>(command.BeneficiaryType, true, out var parsed)
            ? parsed
            : cause?.BeneficiaryType ?? BeneficiaryType.Platform;

        var now = _clock.UtcNow;
        var amount = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);

        var donation = Donation.Pledge(
            DonationId.From(_guids.NewSequentialGuid()),
            caller.Value,
            command.IsAnonymous,
            beneficiaryType,
            amount,
            _receipts.Next("DON", now),
            now,
            command.BeneficiaryId ?? cause?.BeneficiaryId,
            cause?.Id,
            cause?.Code,
            command.Message,
            command.CountryCode ?? cause?.CountryCode,
            command.RecurringIntervalMonths);

        _donations.Add(donation);

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            facts.Code,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        var methodKind = Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var method)
            ? method
            : PaymentMethodKind.Card;

        var intent = PaymentIntent.Create(
            PaymentIntentId.From(_guids.NewSequentialGuid()),
            caller.Value,
            account.Id,
            amount,
            methodKind,
            cause is null ? "Donation" : $"Donation: {cause.Title}",
            now,
            null,
            command.IdempotencyKey ?? $"donation:{donation.Id.Value:N}",
            command.CountryCode);

        _intents.Add(intent);
        donation.AttachPayment(intent.Id);

        var scheduled = await ScheduleRecurrenceAsync(
            command, caller.Value, cause, amount, donation, now, cancellationToken);

        if (scheduled.IsFailure)
        {
            return Result.Failure<DonationResponse>(scheduled.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(donation, intent, facts.Symbol));
    }

    /// <summary>
    /// Answers a retried donation with the donation it already made.
    /// <para>
    /// <b>This closes a live defect rather than adding a feature, and the defect was silent.</b>
    /// <c>POST /financial/donations</c> is documented as idempotent — the frontend's own register
    /// says "B15 uniquely indexes <c>IdempotencyKey</c> on payment intents, purchases, subscriptions,
    /// donations and contributions", and the client persists a donation key to storage that survives
    /// an app restart. What this handler actually did was pass the key straight through to the
    /// payment intent it opened and create a second donation, so the <em>second</em> request hit the
    /// unique index on the intent and came back <b>500</b>. A member who double-tapped Give, or
    /// whose phone retried on a flaky connection, was told the platform had broken.
    /// </para>
    /// <para>
    /// <b>The lookup is B15's own primitive rather than a second one.</b> The donation's intent
    /// carries the caller's key verbatim, so finding the intent finds the donation. No new column and
    /// no new store — which is what the register asks for in as many words: copy the primitive, do
    /// not invent one.
    /// </para>
    /// <para>
    /// <b>A key that belongs to somebody else, or to a payment that was not a donation, is a
    /// conflict rather than a replay.</b> Handing back a donation the caller did not make would be a
    /// receipt for money that was not theirs.
    /// </para>
    /// </summary>
    private async Task<Result<DonationResponse>?> ReplayAsync(
        CreateDonationCommand command,
        Guid callerUserId,
        string? symbol,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            return null;
        }

        var intent = await _intents.FindByIdempotencyKeyAsync(
            callerUserId, command.IdempotencyKey, cancellationToken);

        if (intent is null)
        {
            return null;
        }

        if (intent.PayerUserId != callerUserId)
        {
            return Result.Failure<DonationResponse>(Error.Conflict(
                FinancialErrorCodes.TransactionDuplicate,
                "That idempotency key belongs to another request."));
        }

        var donation = await _donations.FindByPaymentIntentAsync(intent.Id, cancellationToken);

        return donation is null
            ? Result.Failure<DonationResponse>(Error.Conflict(
                FinancialErrorCodes.TransactionDuplicate,
                "That idempotency key was used for a payment that was not a donation."))
            : Result.Success(FinancialMapper.ToResponse(donation, intent, symbol));
    }

    /// <summary>
    /// Turns <c>recurringIntervalMonths</c> into a real schedule. COMMUNITY-LIFE-GAP-008.
    /// <para>
    /// <b>Staged in the same unit of work as the donation.</b> A schedule saved separately could
    /// exist for a donation that failed to save, and the donor would be charged monthly for a first
    /// gift that never happened.
    /// </para>
    /// <para>
    /// <b>An anonymous recurring donation is refused rather than silently made one-off.</b> B15
    /// stores no donor identifier on an anonymous gift, so there would be nobody to charge next month
    /// and nobody to tell when it failed — and quietly dropping the interval is how a member ends up
    /// believing they set up a monthly gift that does not exist. The refusal names what to change.
    /// </para>
    /// <para>
    /// <b>A cause is required.</b> A recurring gift with no cause has no target to give to next
    /// month; the one-off form of that is a donation to the platform, which is a single act rather
    /// than a standing instruction.
    /// </para>
    /// </summary>
    private async Task<Result> ScheduleRecurrenceAsync(
        CreateDonationCommand command,
        Guid donorUserId,
        DonationCause? cause,
        Money amount,
        Donation donation,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (command.RecurringIntervalMonths is not { } interval || interval <= 0)
        {
            return Result.Success();
        }

        if (!_options.EnableRecurringDonations)
        {
            // Declared and not scheduled, exactly as B15 shipped it. The donation still stands.
            return Result.Success();
        }

        if (command.IsAnonymous)
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.RecurringDonationIntervalInvalid,
                "An anonymous donation cannot repeat: Diaspora records nobody to charge again. "
                + "Give anonymously once, or set up a repeating gift under your name."));
        }

        if (cause is null)
        {
            return Result.Failure(Error.Validation(
                FinancialErrorCodes.RecurringDonationIntervalInvalid,
                "A repeating donation needs a cause to repeat against."));
        }

        var schedule = RecurringDonation.Schedule(
            RecurringDonationId.From(_guids.NewSequentialGuid()),
            donorUserId,
            cause,
            amount,
            interval,
            now,
            donation.Id,
            command.IdempotencyKey is { } key ? $"recurring:{key}" : null);

        if (schedule.IsFailure)
        {
            return Result.Failure(schedule.Error);
        }

        // A retried donation carries the same idempotency key, so the schedule's derived key finds
        // the one already created rather than opening a second standing instruction.
        if (schedule.Value.IdempotencyKey is { } scheduleKey
            && await _schedules.FindByIdempotencyKeyAsync(
                schedule.Value.DonorUserId, scheduleKey, cancellationToken) is not null)
        {
            return Result.Success();
        }

        _schedules.Add(schedule.Value);

        return Result.Success();
    }
}
