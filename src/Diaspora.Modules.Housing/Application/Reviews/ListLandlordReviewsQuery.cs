using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reviews;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Reviews;

/// <summary>
/// A landlord's reviews, with the rollup that belongs beside them.
/// <para>
/// <b>Readable without an account.</b> Somebody deciding whether to trust a landlord with a
/// deposit is often deciding whether to join the platform at all.
/// </para>
/// <para>
/// No <c>[Authorize]</c>, which is this module's way of saying a query is public. The endpoint
/// carries <c>.AllowAnonymous()</c> to match.
/// </para>
/// </summary>
[AllowAnonymous(
    "Public discovery. The route already says AllowAnonymous and this is the same statement at " +
    "the other end of the pipeline: a listing nobody can see without signing up is a listing " +
    "nobody answers.")]
public sealed record ListLandlordReviewsQuery : IQuery<LandlordReviewPageResponse>
{
    public required Guid LandlordId { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = HousingLimits.DefaultPageSize;
}

public sealed class ListLandlordReviewsQueryHandler
    : IQueryHandler<ListLandlordReviewsQuery, LandlordReviewPageResponse>
{
    private readonly ILandlordProfileRepository _landlords;
    private readonly ILandlordReviewRepository _reviews;
    private readonly IClock _clock;

    public ListLandlordReviewsQueryHandler(
        ILandlordProfileRepository landlords, ILandlordReviewRepository reviews, IClock clock)
    {
        _landlords = landlords;
        _reviews = reviews;
        _clock = clock;
    }

    public async Task<Result<LandlordReviewPageResponse>> Handle(
        ListLandlordReviewsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var landlordId = LandlordProfileId.From(request.LandlordId);
        var landlord = await _landlords.GetForReadAsync(landlordId, cancellationToken);

        if (landlord is null || landlord.IsDeleted)
        {
            return Result.Failure<LandlordReviewPageResponse>(HousingErrors.LandlordNotFound);
        }

        var page = await _reviews.ListForLandlordAsync(
            landlordId, request.Cursor, request.Limit, cancellationToken);

        var now = _clock.UtcNow;

        // The rollup travels with the page rather than needing a second call, because every
        // surface that renders the list renders the average above it.
        return Result.Success(new LandlordReviewPageResponse
        {
            Rating = landlord.ReviewSummary.ToResponse(),
            Items = page.Items.Select(review => review.ToResponse(now)).ToList(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }
}
