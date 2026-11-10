using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.Reviews;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Housing.Application.Reviews;

/// <summary>
/// Writes or revises a tenant's review of a tenancy.
/// <para>
/// One command for both because the client offers one control: a tenant opens the tenancy and
/// either rates it or changes the rating they gave.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ReviewWrite)]
[Transactional]
public sealed record WriteLandlordReviewCommand : ICommand<LandlordReviewResponse>
{
    public required Guid LandlordId { get; init; }

    public required WriteLandlordReviewRequest Request { get; init; }
}

public sealed class WriteLandlordReviewCommandValidator
    : AbstractValidator<WriteLandlordReviewCommand>
{
    public WriteLandlordReviewCommandValidator()
    {
        RuleFor(command => command.LandlordId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.ReservationId).NotEmpty();

        RuleFor(command => command.Request.Rating)
            .InclusiveBetween(LandlordReviewSummary.MinRating, LandlordReviewSummary.MaxRating);

        RuleFor(command => command.Request.Title).MaximumLength(LandlordReview.MaxTitleLength);
        RuleFor(command => command.Request.Body).MaximumLength(LandlordReview.MaxBodyLength);
    }
}

/// <summary>
/// Writes a review, having first established that there is a tenancy to review.
/// <para>
/// <b>The gate is the whole feature.</b> A star above a deposit is worth exactly as much as the
/// check behind it, so this handler refuses unless the caller holds a reservation with this
/// landlord in <see cref="HousingReservationStatus.Occupied"/> — this module's own words for
/// that state are "moved in… the successful terminal state". <c>LeaseSigned</c> is deliberately
/// not enough: a signed lease is a promise, and a tenancy that fell through before move-in is
/// not an experience of living somewhere.
/// </para>
/// </summary>
public sealed class WriteLandlordReviewCommandHandler
    : ICommandHandler<WriteLandlordReviewCommand, LandlordReviewResponse>
{
    private readonly ILandlordProfileRepository _landlords;
    private readonly ILandlordReviewRepository _reviews;
    private readonly IHousingReservationRepository _reservations;
    private readonly LandlordReviewSummaryService _summaries;
    private readonly HousingAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public WriteLandlordReviewCommandHandler(
        ILandlordProfileRepository landlords,
        ILandlordReviewRepository reviews,
        IHousingReservationRepository reservations,
        LandlordReviewSummaryService summaries,
        HousingAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _landlords = landlords;
        _reviews = reviews;
        _reservations = reservations;
        _summaries = summaries;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<LandlordReviewResponse>> Handle(
        WriteLandlordReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<LandlordReviewResponse>(caller.Error);
        }

        var landlordId = LandlordProfileId.From(command.LandlordId);
        var landlord = await _landlords.GetAsync(landlordId, cancellationToken);

        if (landlord is null || landlord.IsDeleted)
        {
            return Result.Failure<LandlordReviewResponse>(HousingErrors.LandlordNotFound);
        }

        var reservationId = HousingReservationId.From(command.Request.ReservationId);
        var reservation = await _reservations.GetAsync(reservationId, cancellationToken);

        // Three separate ways this can be the wrong reservation, checked together because
        // telling them apart would let a caller probe for other people's tenancies: it must
        // exist, it must be theirs, and it must be with this landlord.
        if (reservation is null
            || reservation.SeekerUserId != caller.Value
            || reservation.LandlordId != landlordId)
        {
            return Result.Failure<LandlordReviewResponse>(HousingErrors.ReviewNoTenancy);
        }

        if (reservation.Status != HousingReservationStatus.Occupied)
        {
            return Result.Failure<LandlordReviewResponse>(HousingErrors.ReviewNoTenancy);
        }

        var now = _clock.UtcNow;
        var existing = await _reviews.FindByReservationAsync(reservationId, cancellationToken);

        LandlordReview review;

        if (existing is null)
        {
            var written = LandlordReview.Write(
                landlordId,
                landlord.OwnerUserId,
                caller.Value,
                reservationId,
                reservation.ListingId,
                command.Request.Rating,
                command.Request.Title,
                command.Request.Body,
                now);

            if (written.IsFailure)
            {
                return Result.Failure<LandlordReviewResponse>(written.Error);
            }

            review = written.Value;
            await _reviews.AddAsync(review, cancellationToken);
        }
        else
        {
            if (existing.ReviewerId != caller.Value)
            {
                return Result.Failure<LandlordReviewResponse>(HousingErrors.ReviewAlreadyWritten);
            }

            var revised = existing.Revise(
                command.Request.Rating, command.Request.Title, command.Request.Body, now);

            if (revised.IsFailure)
            {
                return Result.Failure<LandlordReviewResponse>(revised.Error);
            }

            review = existing;
        }

        // Saved before the rollup is recomputed, and again after. The order is not stylistic:
        // the summary service recomputes from a GROUP BY over the reviews table, and a review
        // still sitting in the change tracker is not in that table yet. Refreshing first would
        // recompute the summary that existed a moment ago.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _summaries.RefreshAsync(landlord, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(review.ToResponse(now));
    }
}
