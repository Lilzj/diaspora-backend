using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Application.Mapping;
using Diaspora.Modules.Recommendations.Application.Services;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;
using Diaspora.Modules.Recommendations.Domain.Analytics;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Recommendations.Application.Analytics;

/// <summary>
/// Reads the rollups.
/// <para>
/// <b>An absent date range is resolved to the last thirty days rather than left open, and that is a
/// deliberate refusal to offer an unbounded query.</b> An aggregate over the whole rollup table is
/// the one request in this module that could take a production database down, and "the caller did
/// not specify" is not a good enough reason to run it.
/// </para>
/// </summary>
public sealed class GetRecommendationAnalyticsQueryHandler
    : IQueryHandler<GetRecommendationAnalyticsQuery, IReadOnlyList<RecommendationAnalyticsResponse>>
{
    /// <summary>The default window. See the class summary.</summary>
    private const int DefaultWindowDays = 30;

    /// <summary>The ceiling a caller cannot ask past.</summary>
    private const int MaximumRows = 365;

    private readonly IRecommendationAnalyticsRepository _analytics;
    private readonly IClock _clock;

    public GetRecommendationAnalyticsQueryHandler(
        IRecommendationAnalyticsRepository analytics, IClock clock)
    {
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<RecommendationAnalyticsResponse>>> Handle(
        GetRecommendationAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        RecommendationSurface? surface = null;

        if (!string.IsNullOrWhiteSpace(query.Surface))
        {
            var parsed = RecommendationAuthorizationService.ParseSurface(query.Surface);

            if (parsed.IsFailure)
            {
                return Result.Failure<IReadOnlyList<RecommendationAnalyticsResponse>>(parsed.Error);
            }

            surface = parsed.Value;
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow.UtcDateTime);

        var rollups = await _analytics.SearchAsync(
            new RecommendationAnalyticsFilter
            {
                Surface = surface,

                // Bounded whatever the caller asked. See the class summary.
                From = query.From ?? today.AddDays(-DefaultWindowDays),
                To = query.To ?? today,
                Limit = Math.Clamp(query.Limit ?? DefaultWindowDays, 1, MaximumRows)
            },
            cancellationToken);

        return Result.Success<IReadOnlyList<RecommendationAnalyticsResponse>>(
            rollups.Select(RecommendationMapper.ToResponse).ToList());
    }
}
