using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Neighborhoods;

/// <summary>
/// Reads one neighbourhood profile, with its published insights and its statistics.
/// <para>
/// <b>Unauthenticated, and that is most of the point of the module.</b> Somebody deciding whether
/// to move to a city needs to know what living in its neighbourhoods is actually like before they
/// commit to anything — before they join the platform, before they book a flight, before they send
/// anybody a deposit. Putting that behind a sign-in would make the module useful only to people who
/// had already decided.
/// </para>
/// <para>
/// Resolves by Location area identifier first and then by slug, because the client's neighbourhood
/// route addresses places by slug and a slug that happens to parse as a GUID does not exist.
/// </para>
/// </summary>
public sealed class GetNeighborhoodProfileQueryHandler
    : IQueryHandler<GetNeighborhoodProfileQuery, NeighborhoodProfileResponse>
{
    /// <summary>
    /// Insights shown on the profile itself. Ten: enough to characterise a place, few enough that
    /// the page is read rather than scrolled. The full list is its own endpoint.
    /// </summary>
    private const int InsightsOnProfile = 10;

    private readonly INeighborhoodProfileRepository _profiles;
    private readonly INeighborhoodInsightRepository _insights;
    private readonly NeighborhoodStatisticsService _statistics;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IClock _clock;

    public GetNeighborhoodProfileQueryHandler(
        INeighborhoodProfileRepository profiles,
        INeighborhoodInsightRepository insights,
        NeighborhoodStatisticsService statistics,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IClock clock)
    {
        _profiles = profiles;
        _insights = insights;
        _statistics = statistics;
        _authorization = authorization;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<NeighborhoodProfileResponse>> Handle(
        GetNeighborhoodProfileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await ResolveAsync(request, cancellationToken);

        if (profile is null)
        {
            return Result.Failure<NeighborhoodProfileResponse>(
                HousingErrors.NeighborhoodProfileNotFound);
        }

        var insights = await _insights.QueryAsync(
            new NeighborhoodInsightQueryFilter
            {
                AreaId = profile.AreaId,
                OrderByEndorsement = true,
                Limit = InsightsOnProfile
            },
            cursor: null,
            InsightsOnProfile,
            cancellationToken);

        var statistics = await _statistics.ForAsync(profile, cancellationToken);

        var rendered = new List<NeighborhoodInsightResponse>(insights.Items.Count);

        foreach (var insight in insights.Items)
        {
            var endorsed = _authorization.CallerId is { } callerId
                && await _insights.HasEndorsedAsync(insight.Id, callerId, cancellationToken);

            rendered.Add(NeighborhoodMapper.ToResponse(
                insight, _authorization.CallerId, _authorization.IsStaff, endorsed));
        }

        return Result.Success(NeighborhoodMapper.ToResponse(
            profile,
            statistics,
            rendered,
            _clock.UtcNow,
            _options.Value.NeighborhoodReviewIntervalDays));
    }

    private async Task<NeighborhoodProfile?> ResolveAsync(
        GetNeighborhoodProfileQuery request, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(request.IdOrSlug, out var areaId))
        {
            var byArea = await _profiles.GetByAreaForReadAsync(areaId, cancellationToken);

            if (byArea is not null)
            {
                return byArea;
            }
        }

        return await _profiles.GetBySlugAsync(
            request.IdOrSlug, request.CountryCode, cancellationToken);
    }
}
