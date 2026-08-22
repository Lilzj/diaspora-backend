using Diaspora.Application.Abstractions;
using MediatR;
using Diaspora.Modules.Providers.Application.Services;
using Diaspora.Modules.Providers.Contracts.V1.Responses;
using Diaspora.Modules.Providers.Domain.Providers;
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
    private readonly IProviderRepository _providers;
    private readonly ProviderAuthorizationService _authorization;
    private readonly ISender _sender;

    public ListMyProviderReviewsQueryHandler(
        IProviderRepository providers,
        ProviderAuthorizationService authorization,
        ISender sender)
    {
        _providers = providers;
        _authorization = authorization;
        _sender = sender;
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

        return await _sender.Send(
            new ListProviderReviewsQuery
            {
                ProviderId = mine[0].Id.Value,
                Cursor = query.Cursor,
                Limit = query.Limit
            },
            cancellationToken);
    }
}
