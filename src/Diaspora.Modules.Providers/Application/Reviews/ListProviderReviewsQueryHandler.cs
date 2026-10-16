using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Reviews;

/// <summary>
/// Serves a page of reviews.
/// <para>
/// The rollup travels with the first page, so the client's provider screen renders its rating
/// bars and its review list from one call rather than two. Attaching it to every page would be
/// redundant, and omitting it entirely would make the first render a waterfall.
/// </para>
/// <para>
/// Hidden and removed reviews are included only for staff and for the provider's own managers —
/// an owner needs to see that a review was hidden, or they will keep asking where it went.
/// </para>
/// </summary>
public sealed class ListProviderReviewsQueryHandler
    : IQueryHandler<ListProviderReviewsQuery, ProviderReviewPageResponse>
{
    private const int DefaultLimit = 20;

    private readonly IProviderRepository _providers;
    private readonly IProviderReviewRepository _reviews;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReviewerNames _names;

    public ListProviderReviewsQueryHandler(
        IProviderRepository providers,
        IProviderReviewRepository reviews,
        ProviderAuthorizationService authorization,
        ProviderReviewerNames names)
    {
        _providers = providers;
        _reviews = reviews;
        _authorization = authorization;
        _names = names;
    }

    public async Task<Result<ProviderReviewPageResponse>> Handle(
        ListProviderReviewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var providerId = ProviderId.From(query.ProviderId);
        var provider = await _providers.GetForReadAsync(providerId, cancellationToken);

        if (provider is null || provider.IsDeleted)
        {
            return Result.Failure<ProviderReviewPageResponse>(ProviderErrors.NotFound);
        }

        var includeHidden = _authorization.IsStaff
            || provider.HasAtLeast(_authorization.CallerId, ProviderRole.Manager);

        var page = await _reviews.ListForProviderAsync(
            providerId,
            query.Cursor,
            query.Limit ?? DefaultLimit,
            includeHidden,
            cancellationToken);

        var names = await _names.ResolveAsync(
            page.Items.Select(review => review.ReviewerId), cancellationToken);

        return Result.Success(new ProviderReviewPageResponse
        {
            Items = page.Items
                .Select(review => ProviderReviewMapper.ToResponse(
                    review, names.GetValueOrDefault(review.ReviewerId.Value)))
                .ToList(),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore,
            Summary = query.Cursor is null
                ? ProviderReviewMapper.ToResponse(provider.ReviewSummary)
                : null,

            // Echoed so a caller that reached here through `/providers/me/reviews` knows which of
            // its providers answered, and so no client has to remember the id it asked for.
            ProviderId = provider.Id.Value
        });
    }
}
