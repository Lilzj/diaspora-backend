using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using FluentValidation;

namespace Diaspora.Modules.Marketplace.Application.Reviews;

/// <summary>
/// Writes or revises a buyer's review of a completed purchase.
/// <para>
/// One command for both because the client offers one control: a buyer opens the purchase and
/// either rates it or changes the rating they gave. Splitting it would make the client ask
/// "have I reviewed this already" before every write, which is a question the server is better
/// placed to answer.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ReviewWrite)]
[Transactional]
public sealed record WriteSellerReviewCommand : ICommand<SellerReviewResponse>
{
    public required Guid SellerId { get; init; }

    public required WriteSellerReviewRequest Request { get; init; }
}

public sealed class WriteSellerReviewCommandValidator : AbstractValidator<WriteSellerReviewCommand>
{
    public WriteSellerReviewCommandValidator()
    {
        RuleFor(command => command.SellerId).NotEmpty();
        RuleFor(command => command.Request).NotNull();

        RuleFor(command => command.Request.ReservationId).NotEmpty();

        RuleFor(command => command.Request.Rating)
            .InclusiveBetween(SellerReviewSummary.MinRating, SellerReviewSummary.MaxRating);

        RuleFor(command => command.Request.Title)
            .MaximumLength(Domain.Reviews.SellerReview.MaxTitleLength);

        RuleFor(command => command.Request.Body)
            .MaximumLength(Domain.Reviews.SellerReview.MaxBodyLength);
    }
}
