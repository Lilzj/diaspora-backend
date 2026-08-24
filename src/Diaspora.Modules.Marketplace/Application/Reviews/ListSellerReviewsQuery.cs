using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Application.Mapping;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Reviews;
using Diaspora.Modules.Marketplace.Domain.Sellers;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Reviews;

/// <summary>
/// A seller's reviews, with the rollup that belongs beside them.
/// <para>
/// <b>Readable without an account.</b> A rating that only signed-in members can see is a rating
/// that cannot inform the decision it exists for — somebody deciding whether to trust a seller
/// is often deciding whether to join at all.
/// </para>
/// <para>
/// No <c>[Authorize]</c>, which is this module's way of saying a query is public — the same as
/// <c>GetListingQuery</c> and the discovery searches. The endpoint carries
/// <c>.AllowAnonymous()</c> to match.
/// </para>
/// </summary>
public sealed record ListSellerReviewsQuery : IQuery<SellerReviewPageResponse>
{
    public required Guid SellerId { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = MarketplaceLimits.DefaultPageSize;
}

public sealed class ListSellerReviewsQueryHandler
    : IQueryHandler<ListSellerReviewsQuery, SellerReviewPageResponse>
{
    private readonly ISellerProfileRepository _sellers;
    private readonly ISellerReviewRepository _reviews;
    private readonly IClock _clock;

    public ListSellerReviewsQueryHandler(
        ISellerProfileRepository sellers, ISellerReviewRepository reviews, IClock clock)
    {
        _sellers = sellers;
        _reviews = reviews;
        _clock = clock;
    }

    public async Task<Result<SellerReviewPageResponse>> Handle(
        ListSellerReviewsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sellerId = SellerProfileId.From(request.SellerId);
        var seller = await _sellers.GetForReadAsync(sellerId, cancellationToken);

        if (seller is null || seller.IsDeleted)
        {
            return Result.Failure<SellerReviewPageResponse>(MarketplaceErrors.SellerNotFound);
        }

        var page = await _reviews.ListForSellerAsync(
            sellerId, request.Cursor, request.Limit, cancellationToken);

        var now = _clock.UtcNow;

        // The rollup travels with the page rather than needing a second call, because every
        // surface that renders the list renders the average above it.
        return Result.Success(new SellerReviewPageResponse
        {
            Rating = seller.ReviewSummary.ToResponse(),
            Items = page.Items.Select(review => review.ToResponse(now)).ToList(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }
}
