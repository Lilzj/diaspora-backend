using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Modules.Marketplace.Domain.Reviews;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Reviews;

/// <summary>
/// Writes or revises a review, having first established that there is something to review.
/// <para>
/// <b>The gate is the whole feature.</b> A star above a payment button is worth exactly as much
/// as the check behind it, so this handler refuses unless the caller holds a reservation with
/// this seller in <see cref="ReservationStatus.Completed"/> — B10's own words for that state
/// are "handed over… the only state that turns held stock into sold stock". Every other
/// possible source of a rating on this screen was an invention, including the hardcoded 4.8
/// the client rendered for every seller on the platform.
/// </para>
/// </summary>
public sealed class WriteSellerReviewCommandHandler
    : ICommandHandler<WriteSellerReviewCommand, SellerReviewResponse>
{
    private readonly ISellerProfileRepository _sellers;
    private readonly ISellerReviewRepository _reviews;
    private readonly IListingReservationRepository _reservations;
    private readonly SellerReviewSummaryService _summaries;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public WriteSellerReviewCommandHandler(
        ISellerProfileRepository sellers,
        ISellerReviewRepository reviews,
        IListingReservationRepository reservations,
        SellerReviewSummaryService summaries,
        MarketplaceAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _sellers = sellers;
        _reviews = reviews;
        _reservations = reservations;
        _summaries = summaries;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<SellerReviewResponse>> Handle(
        WriteSellerReviewCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<SellerReviewResponse>(caller.Error);
        }

        var sellerId = SellerProfileId.From(command.SellerId);
        var seller = await _sellers.GetAsync(sellerId, cancellationToken);

        if (seller is null || seller.IsDeleted)
        {
            return Result.Failure<SellerReviewResponse>(MarketplaceErrors.SellerNotFound);
        }

        var reservationId = ListingReservationId.From(command.Request.ReservationId);
        var reservation = await _reservations.GetAsync(reservationId, cancellationToken);

        // Three separate ways this can be the wrong reservation, and they are checked together
        // because telling them apart would let a caller probe for other people's purchases:
        // it must exist, it must be theirs, and it must be with this seller.
        if (reservation is null
            || reservation.BuyerUserId != caller.Value
            || reservation.SellerId != sellerId)
        {
            return Result.Failure<SellerReviewResponse>(MarketplaceErrors.ReviewNoCompletedPurchase);
        }

        if (reservation.Status != ReservationStatus.Completed)
        {
            return Result.Failure<SellerReviewResponse>(MarketplaceErrors.ReviewNoCompletedPurchase);
        }

        var now = _clock.UtcNow;
        var existing = await _reviews.FindByReservationAsync(reservationId, cancellationToken);

        SellerReview review;

        if (existing is null)
        {
            var written = SellerReview.Write(
                sellerId,
                seller.OwnerUserId,
                caller.Value,
                reservationId,
                reservation.ListingId,
                command.Request.Rating,
                command.Request.Title,
                command.Request.Body,
                now);

            if (written.IsFailure)
            {
                return Result.Failure<SellerReviewResponse>(written.Error);
            }

            review = written.Value;
            await _reviews.AddAsync(review, cancellationToken);
        }
        else
        {
            // Somebody else's review of a purchase that is not theirs cannot arrive here — the
            // reservation was already matched to the caller — but the check is kept because it
            // costs nothing and the alternative is trusting an inference.
            if (existing.ReviewerId != caller.Value)
            {
                return Result.Failure<SellerReviewResponse>(MarketplaceErrors.ReviewAlreadyWritten);
            }

            var revised = existing.Revise(
                command.Request.Rating, command.Request.Title, command.Request.Body, now);

            if (revised.IsFailure)
            {
                return Result.Failure<SellerReviewResponse>(revised.Error);
            }

            review = existing;
        }

        // Saved before the rollup is recomputed, and again after. The order is not stylistic:
        // the summary service recomputes from a GROUP BY over the reviews table, and a review
        // still sitting in the change tracker is not in that table yet. Refreshing first would
        // recompute the summary that existed a moment ago — leaving the seller showing "no
        // reviews" on the very request that added one, and drifting further with every edit.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _summaries.RefreshAsync(seller, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(review.ToResponse(now));
    }
}
