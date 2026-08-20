using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Extensibility.Fraud;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>
/// Creates a payment intent, after the fraud assessors have had a look.
/// <para>
/// <b>The idempotency check answers with the original intent rather than refusing.</b> A
/// double-tapped pay button and a retried mobile request are the same request twice; returning the
/// first intent is what the caller wanted, and a 409 would leave a client with a payment it cannot
/// find.
/// </para>
/// <para>
/// <b>Fraud assessment runs before the gateway and the strictest decision wins.</b> Assessors are
/// additive so a future model runs alongside the shipped velocity rules rather than replacing them;
/// a refusal is a 403 naming the fraud code, so support can tell it apart from a decline.
/// </para>
/// </summary>
public sealed class CreatePaymentIntentCommandHandler
    : ICommandHandler<CreatePaymentIntentCommand, PaymentIntentResponse>
{
    private readonly IPaymentIntentRepository _intents;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IEnumerable<IFraudAssessor> _fraud;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePaymentIntentCommandHandler(
        IPaymentIntentRepository intents,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IEnumerable<IFraudAssessor> fraud,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _intents = intents;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _fraud = fraud;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PaymentIntentResponse>> Handle(
        CreatePaymentIntentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(caller.Error);
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        if (!await _currencies.IsSupportedAsync(command.Currency, cancellationToken))
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{command.Currency}' is not a currency this deployment takes payments in."));
        }

        if (command.IdempotencyKey is { } key
            && await _intents.FindByIdempotencyKeyAsync(caller.Value, key, cancellationToken) is { } existing)
        {
            return Result.Success(FinancialMapper.ToResponse(existing, facts.Symbol));
        }

        if (!Enum.TryParse<PaymentMethodKind>(command.MethodKind, true, out var methodKind))
        {
            return Result.Failure<PaymentIntentResponse>(Error.Validation(
                FinancialErrorCodes.PaymentMethodUnsupported,
                $"'{command.MethodKind}' is not a payment method the platform recognises."));
        }

        var amount = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);
        var resolved = ResolveSubject(command);

        if (resolved.IsFailure)
        {
            return Result.Failure<PaymentIntentResponse>(resolved.Error);
        }

        var subject = resolved.Value;

        var assessment = await AssessAsync(caller.Value, amount, methodKind, subject, command, cancellationToken);

        if (assessment.Decision == FraudDecision.Refuse)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Forbidden(
                FinancialErrorCodes.FraudRefused,
                "This payment cannot be taken right now. Contact support if you think that is wrong."));
        }

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            facts.Code,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        if (!account.CanDebit && account.Status != FinancialAccountStatus.Pending)
        {
            return Result.Failure<PaymentIntentResponse>(Error.Forbidden(
                FinancialErrorCodes.AccountRestricted,
                account.RestrictionReason ?? "This account cannot make payments right now."));
        }

        var intent = PaymentIntent.Create(
            PaymentIntentId.From(_guids.NewSequentialGuid()),
            caller.Value,
            account.Id,
            amount,
            methodKind,
            command.Description,
            _clock.UtcNow,
            subject,
            command.IdempotencyKey,
            command.CountryCode);

        _intents.Add(intent);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(intent, facts.Symbol));
    }

    private async Task<FraudAssessment> AssessAsync(
        Guid payerUserId,
        Money amount,
        PaymentMethodKind methodKind,
        SubjectReference? subject,
        CreatePaymentIntentCommand command,
        CancellationToken cancellationToken)
    {
        var recent = await _intents.ListByPayerAsync(payerUserId, null, 50, cancellationToken);
        var hourAgo = _clock.UtcNow.AddHours(-1);
        var dayAgo = _clock.UtcNow.AddDays(-1);

        var context = new FraudContext
        {
            PayerUserId = payerUserId,
            Amount = amount,
            MethodKind = methodKind,
            Subject = subject,
            CountryCode = command.CountryCode,
            RecentAttemptCount = recent.Count(intent => intent.CreatedAt >= hourAgo),
            RecentVolumeMinorUnits = recent
                .Where(intent => intent.CapturedAt >= dayAgo)
                .Sum(intent => intent.CapturedMinorUnits),
            IsVerified = _authorization.Has(FinancialPermissions.PaymentsManage),
            AsOf = _clock.UtcNow
        };

        var worst = FraudAssessment.Allow();

        foreach (var assessor in _fraud)
        {
            var assessment = await assessor.AssessAsync(context, cancellationToken);

            if (assessment.Decision > worst.Decision)
            {
                worst = assessment;
            }
        }

        return worst;
    }

    /// <summary>
    /// Turns the caller's subject into a reference, or refuses.
    /// <para>
    /// <b>A subject this platform does not recognise is a 400, and until B15.1 it was silence.</b>
    /// The previous form returned null on an unparsed type, so the client's
    /// <c>subjectType: "CommunityMembership"</c> — sent on every membership checkout since Phase 12
    /// shipped — produced an intent with no subject at all, no error anywhere, and a ledger with
    /// nothing to reconcile a membership against. The register recorded that "B15 stores them and
    /// does nothing with them"; it did not store them.
    /// </para>
    /// <para>
    /// <b><see cref="Enum.TryParse{T}(string, bool, out T)"/> accepts any number.</b>
    /// <c>subjectType: "97"</c> parses to <c>(MonetizationSubjectType)97</c> and succeeds, which
    /// would put a value no member of this enum has on a payment intent and into every consumer's
    /// <c>ToString()</c>. <see cref="Enum.IsDefined{T}(T)"/> is what closes that, and it has to be
    /// asked separately because parsing is what accepted the number.
    /// </para>
    /// <para>
    /// <b>Half a subject is refused rather than dropped.</b> A type with no identifier and an
    /// identifier with no type are both a client bug, and answering them with an unsubjected payment
    /// hides it until somebody asks what the money bought.
    /// </para>
    /// </summary>
    private static Result<SubjectReference?> ResolveSubject(CreatePaymentIntentCommand command)
    {
        var hasType = !string.IsNullOrWhiteSpace(command.SubjectType);
        var hasId = command.SubjectId is { } candidate && candidate != Guid.Empty;

        if (!hasType && !hasId)
        {
            return Result.Success<SubjectReference?>(null);
        }

        if (!hasType || !hasId)
        {
            return Result.Failure<SubjectReference?>(Error.Validation(
                FinancialErrorCodes.SubjectInvalid,
                "A payment subject is a type and an identifier. Send both or neither."));
        }

        if (!Enum.TryParse<MonetizationSubjectType>(command.SubjectType, true, out var subjectType)
            || !Enum.IsDefined(subjectType))
        {
            return Result.Failure<SubjectReference?>(Error.Validation(
                FinancialErrorCodes.SubjectInvalid,
                $"'{command.SubjectType}' is not a subject this platform recognises."));
        }

        return Result.Success<SubjectReference?>(
            SubjectReference.Create(subjectType, command.SubjectId!.Value));
    }
}
