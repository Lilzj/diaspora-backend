using Diaspora.Application.Abstractions;
using Diaspora.Modules.Providers.Application.Mapping;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Providers.Application.Reviews;

/// <summary>
/// Reviews left on the provider the caller runs.
/// <para>
/// The gap this closes: <c>GET /providers/me/reviews</c> answered a bare 404, so the creator
/// profile has said *"Diaspora cannot list your reviews yet"* while
/// <c>GET /providers/{providerId}/reviews</c> worked perfectly. Nothing mapped a member to the
/// provider they own, so the client had no id to put in the working route.
/// </para>
/// </summary>
[Authorize(Policy = ProviderPolicies.Read)]
public sealed record ListMyProviderReviewsQuery : IQuery<ProviderReviewPageResponse>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}

/// <summary>
/// Resolves the caller's provider and then delegates.
/// <para>
/// <b>The first provider they hold a role on, and the response says which.</b> A member may run
/// more than one — the per-owner ceiling is above one — and picking silently would show somebody
/// their café's reviews under their plumbing business's heading. The response carries
/// <c>providerId</c>, so a screen with more than one can offer a picker and read the rest through
/// the per-provider route, which has always worked.
/// </para>
/// <para>
/// <b>A member who runs nothing gets an empty page, not a 404.</b> "You have no reviews" and "you
/// are not a provider" are different sentences and neither is an error; a null <c>providerId</c>
/// tells the screen which one to render.
/// </para>
/// </summary>
public sealed class ListMyProviderReviewsQueryHandler
    : IQueryHandler<ListMyProviderReviewsQuery, ProviderReviewPageResponse>
{
    private const int DefaultLimit = 20;

    private readonly IProviderRepository _providers;
    private readonly IProviderReviewRepository _reviews;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ProviderReviewerNames _names;

    public ListMyProviderReviewsQueryHandler(
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
        ListMyProviderReviewsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<ProviderReviewPageResponse>(caller.Error);
        }

        var mine = await _providers.ListForMemberAsync(caller.Value, limit: 1, cancellationToken);

        if (mine.Count == 0)
        {
            return Result.Success(ProviderReviewPageResponse.Empty());
        }

        // ── Reads the page directly rather than sending to the sibling handler ──
        //
        // This used to inject `ISender` and dispatch `ListProviderReviewsQuery`.
        // DSP1001 forbids that — a handler that depends on the mediator can reach
        // any other handler, so the call graph stops being visible in the type
        // and a cycle stops being a compile-time impossibility.
        //
        // It was a **warning** in Debug and an **error** in Release, and every
        // local build here is Debug, so it only surfaced when the container built
        // with `-c Release`. Nothing had ever compiled this solution the way it
        // ships.
        //
        // The provider is already loaded above, so doing the work here costs one
        // repository call and no second lookup — the sibling handler would have
        // re-fetched the provider it was handed the id of.
        var provider = mine[0];

        var includeHidden = _authorization.IsStaff
            || provider.HasAtLeast(_authorization.CallerId, ProviderRole.Manager);

        var page = await _reviews.ListForProviderAsync(
            provider.Id,
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
            ProviderId = provider.Id.Value
        });
    }
}
