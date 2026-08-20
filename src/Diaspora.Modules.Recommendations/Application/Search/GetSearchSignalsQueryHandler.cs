using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Search;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Search;

/// <summary>
/// Serves the search integration.
/// <para>
/// <b>Two shapes behind one query, because they have different privacy properties.</b> The
/// personalised boosts are about the caller and require them to be signed in; the trending boosts are
/// about a country and are the same for everybody, which is what makes them cacheable and cheap.
/// </para>
/// </summary>
public sealed class GetSearchSignalsQueryHandler
    : IQueryHandler<GetSearchSignalsQuery, IReadOnlyList<SearchBoostResponse>>
{
    private readonly ISearchSignalProvider _signals;
    private readonly RecommendationAuthorizationService _authorization;

    public GetSearchSignalsQueryHandler(
        ISearchSignalProvider signals, RecommendationAuthorizationService authorization)
    {
        _signals = signals;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<SearchBoostResponse>>> Handle(
        GetSearchSignalsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Trending)
        {
            RecommendationTargetType? targetType = null;

            if (!string.IsNullOrWhiteSpace(query.TargetType))
            {
                var parsed = RecommendationAuthorizationService.ParseTargetType(query.TargetType);

                if (parsed.IsFailure)
                {
                    return Result.Failure<IReadOnlyList<SearchBoostResponse>>(parsed.Error);
                }

                targetType = parsed.Value;
            }

            var trending = await _signals.GetTrendingSignalsAsync(
                query.CountryCode, targetType, cancellationToken);

            return Result.Success<IReadOnlyList<SearchBoostResponse>>(
                trending.Select(RecommendationMapper.ToResponse).ToList());
        }

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<SearchBoostResponse>>(caller.Error);
        }

        var boosts = await _signals.GetBoostSignalsAsync(caller.Value, cancellationToken);

        return Result.Success<IReadOnlyList<SearchBoostResponse>>(
            boosts.Select(RecommendationMapper.ToResponse).ToList());
    }
}
