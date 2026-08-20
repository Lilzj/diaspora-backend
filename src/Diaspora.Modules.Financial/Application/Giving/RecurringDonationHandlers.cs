using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Giving;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Financial.Application.Giving;

/// <summary>
/// The caller's own recurring donations. COMMUNITY-LIFE-GAP-008.
/// <para>
/// Every state, cancelled ones included: a donor who stopped a monthly gift is entitled to see that
/// it stopped, and a list that hid it would look as though the cancellation had lost the record.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingRead)]
public sealed record GetMyRecurringDonationsQuery : IQuery<IReadOnlyList<RecurringDonationResponse>>;

public sealed class GetMyRecurringDonationsQueryHandler
    : IQueryHandler<GetMyRecurringDonationsQuery, IReadOnlyList<RecurringDonationResponse>>
{
    private readonly IRecurringDonationRepository _schedules;
    private readonly IDonationCauseRepository _causes;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetMyRecurringDonationsQueryHandler(
        IRecurringDonationRepository schedules,
        IDonationCauseRepository causes,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _schedules = schedules;
        _causes = causes;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<RecurringDonationResponse>>> Handle(
        GetMyRecurringDonationsQuery query, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<RecurringDonationResponse>>(caller.Error);
        }

        var schedules = await _schedules.ListByDonorAsync(caller.Value, cancellationToken);
        var symbols = await _currencies.AllAsync(cancellationToken);
        var titles = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var code in schedules.Select(schedule => schedule.CauseCode).Distinct(StringComparer.Ordinal))
        {
            if (await _causes.FindByCodeAsync(code, cancellationToken) is { } cause)
            {
                titles[code] = cause.Title;
            }
        }

        return Result.Success<IReadOnlyList<RecurringDonationResponse>>(
        [
            .. schedules.Select(schedule => RecurringDonationMapper.ToResponse(
                schedule,
                titles.TryGetValue(schedule.CauseCode, out var title) ? title : null,
                symbols.TryGetValue(schedule.Currency, out var facts) ? facts.Symbol : null))
        ]);
    }
}

/// <summary>
/// Stops a recurring donation. COMMUNITY-LIFE-GAP-008.
/// <para>
/// <b>Only the owner, and idempotent.</b> A cancel button tapped twice is one instruction; answering
/// the second with a 409 would tell somebody their cancellation failed when it had already worked.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.GivingCreate)]
[Transactional(Reason = "The schedule is stopped and its next charge cleared in one write.")]
[Auditable(Action = "financial.recurring_donation.cancel")]
public sealed record CancelRecurringDonationCommand : ICommand<RecurringDonationResponse>
{
    public required Guid RecurringDonationId { get; init; }
}

public sealed class CancelRecurringDonationCommandValidator
    : AbstractValidator<CancelRecurringDonationCommand>
{
    public CancelRecurringDonationCommandValidator() =>
        RuleFor(command => command.RecurringDonationId).NotEmpty();
}

public sealed class CancelRecurringDonationCommandHandler
    : ICommandHandler<CancelRecurringDonationCommand, RecurringDonationResponse>
{
    private readonly IRecurringDonationRepository _schedules;
    private readonly IDonationCauseRepository _causes;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public CancelRecurringDonationCommandHandler(
        IRecurringDonationRepository schedules,
        IDonationCauseRepository causes,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _schedules = schedules;
        _causes = causes;
        _authorization = authorization;
        _currencies = currencies;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RecurringDonationResponse>> Handle(
        CancelRecurringDonationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RecurringDonationResponse>(caller.Error);
        }

        var schedule = await _schedules.FindAsync(
            RecurringDonationId.From(command.RecurringDonationId), cancellationToken);

        // Absent and somebody else's are answered identically, so a caller cannot enumerate which
        // schedule identifiers exist.
        if (schedule is null || schedule.DonorUserId != caller.Value)
        {
            return Result.Failure<RecurringDonationResponse>(Error.NotFound(
                FinancialErrorCodes.RecurringDonationNotFound,
                "No recurring donation of yours with that identifier."));
        }

        var cancelled = schedule.Cancel(_clock.UtcNow);

        if (cancelled.IsFailure)
        {
            return Result.Failure<RecurringDonationResponse>(cancelled.Error);
        }

        _schedules.Update(schedule);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cause = await _causes.FindByCodeAsync(schedule.CauseCode, cancellationToken);

        return Result.Success(RecurringDonationMapper.ToResponse(
            schedule,
            cause?.Title,
            await _currencies.SymbolAsync(schedule.Currency, cancellationToken)));
    }
}

/// <summary>Turns a schedule into the client's shape.</summary>
internal static class RecurringDonationMapper
{
    public static RecurringDonationResponse ToResponse(
        RecurringDonation schedule, string? causeTitle, string? symbol)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        return new RecurringDonationResponse
        {
            RecurringDonationId = schedule.Id.Value,
            CauseCode = schedule.CauseCode,
            CauseTitle = causeTitle,
            Amount = FinancialMapper.ToResponse(schedule.Amount, symbol),
            IntervalMonths = schedule.IntervalMonths,
            Status = schedule.Status.ToString(),
            NextChargeAt = schedule.NextChargeAt,
            LastChargeAt = schedule.LastChargeAt,
            LastAttemptAt = schedule.LastAttemptAt,
            LastFailureReason = schedule.LastFailureReason,
            ConsecutiveFailures = schedule.ConsecutiveFailures,
            ChargeCount = schedule.ChargeCount,
            CreatedAt = schedule.CreatedAt,
            CancelledAt = schedule.CancelledAt
        };
    }
}
