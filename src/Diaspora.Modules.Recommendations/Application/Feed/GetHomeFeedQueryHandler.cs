using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Feed;
using Diaspora.Modules.Recommendations.Infrastructure.Personalization;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Feed;

/// <summary>
/// Builds the home feed.
/// <para>
/// <b>Thin on purpose: resolve the caller, assemble the context once, ask the composer, map.</b>
/// Every decision worth arguing about — which sections, in what order, ranked how — lives in the
/// composer, the catalogue and the engine, which is what makes them replaceable and testable
/// without an HTTP request.
/// </para>
/// <para>
/// <b>The context is built here and passed down rather than rebuilt per section.</b> Eight sections
/// each resolving the profile independently would be eight reads of one row per home screen, and two
/// of them could disagree if a fold committed in between.
/// </para>
/// </summary>
public sealed class GetHomeFeedQueryHandler : IQueryHandler<GetHomeFeedQuery, HomeFeedResponse>
{
    private readonly PersonalizationContextFactory _contexts;
    private readonly IFeedComposer _composer;
    private readonly RecommendationAuthorizationService _authorization;

    public GetHomeFeedQueryHandler(
        PersonalizationContextFactory contexts,
        IFeedComposer composer,
        RecommendationAuthorizationService authorization)
    {
        _contexts = contexts;
        _composer = composer;
        _authorization = authorization;
    }

    public async Task<Result<HomeFeedResponse>> Handle(
        GetHomeFeedQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<HomeFeedResponse>(caller.Error);
        }

        var context = await _contexts.CreateAsync(
            caller.Value,
            RecommendationSurface.HomeFeed,
            _authorization.ResolveTier(),
            targetType: null,
            query.CountryCode,
            cancellationToken);

        var sections = await _composer.ComposeAsync(context, cancellationToken);

        return Result.Success(new HomeFeedResponse
        {
            Sections = sections.Select(RecommendationMapper.ToResponse).ToList(),

            // What the client surfaces to the member. See HomeFeedResponse.
            IsPersonalized = context.IsPersonalizationAllowed,
            IsColdStart = context.IsColdStart,
            ProfileCompleteness = context.Profile.Completeness,
            GeneratedAt = context.AsOf
        });
    }
}
