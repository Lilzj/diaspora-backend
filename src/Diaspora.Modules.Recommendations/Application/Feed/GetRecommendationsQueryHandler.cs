using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Application.Feed;

/// <summary>
/// Serves every per-vertical recommendation surface.
/// <para>
/// <b>The whole handler is a parse, an engine call and a map, and it should stay that way.</b> Any
/// per-surface behaviour that appears here is behaviour the catalogue should be expressing instead —
/// which is the difference between a platform an operator can tune and one that needs a release to
/// change what a screen shows.
/// </para>
/// <para>
/// <b>The target type is validated against the surface only loosely.</b> Asking the jobs surface for
/// housing is a client bug, but refusing it would also refuse the legitimate case of a mixed surface
/// narrowed to one type. The candidate pool answers with nothing, which is honest and does not need
/// a rule.
/// </para>
/// </summary>
public sealed class GetRecommendationsQueryHandler
    : IQueryHandler<GetRecommendationsQuery, RecommendationPageResponse>
{
    private readonly IRecommendationEngine _engine;
    private readonly RecommendationAuthorizationService _authorization;
    private readonly IOptions<RecommendationOptions> _options;
    private readonly IClock _clock;

    public GetRecommendationsQueryHandler(
        IRecommendationEngine engine,
        RecommendationAuthorizationService authorization,
        IOptions<RecommendationOptions> options,
        IClock clock)
    {
        _engine = engine;
        _authorization = authorization;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<RecommendationPageResponse>> Handle(
        GetRecommendationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<RecommendationPageResponse>(caller.Error);
        }

        var surface = RecommendationAuthorizationService.ParseSurface(query.Surface);

        if (surface.IsFailure)
        {
            return Result.Failure<RecommendationPageResponse>(surface.Error);
        }

        RecommendationTargetType? targetType = null;

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            var parsed = RecommendationAuthorizationService.ParseTargetType(query.TargetType);

            if (parsed.IsFailure)
            {
                return Result.Failure<RecommendationPageResponse>(parsed.Error);
            }

            targetType = parsed.Value;
        }

        var options = _options.Value;

        var result = await _engine.GenerateAsync(
            new RecommendationRequest
            {
                UserId = caller.Value,
                Surface = surface.Value,
                TargetType = targetType,
                Limit = Math.Clamp(
                    query.Limit ?? options.DefaultPageSize, 1, options.MaximumPageSize),
                AccessTier = _authorization.ResolveTier(),
                CountryCodeOverride = query.CountryCode,
                CategoryKeys = query.Categories,
                ForceRegenerate = query.Refresh
            },
            cancellationToken);

        return Result.Success(
            RecommendationMapper.ToResponse(result, surface.Value, _clock.UtcNow));
    }
}
