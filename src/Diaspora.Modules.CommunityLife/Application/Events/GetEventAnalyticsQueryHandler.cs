using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// What an organizer sees about how their event performed.
/// <para>
/// <b>Only the organizer and the body behind them.</b> Attendance figures say who came and who did
/// not, which is a fact about named members; showing them beside a public event page would let
/// anybody infer who was absent.
/// </para>
/// <para>
/// A missing record returns zeros rather than failing — it is opened at creation, so this covers
/// only rows that predate that.
/// </para>
/// </summary>
public sealed class GetEventAnalyticsQueryHandler
    : IQueryHandler<GetEventAnalyticsQuery, EventAnalyticsResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly CommunityLifeAuthorizationService _authorization;

    public GetEventAnalyticsQueryHandler(
        ICommunityEventRepository events,
        IEventAnalyticsRepository analytics,
        CommunityLifeAuthorizationService authorization)
    {
        _events = events;
        _analytics = analytics;
        _authorization = authorization;
    }

    public async Task<Result<EventAnalyticsResponse>> Handle(
        GetEventAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(query.EventId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<EventAnalyticsResponse>(CommunityLifeErrors.EventNotFound);
        }

        var caller = await _authorization.RequireEventWriteAsync(
            communityEvent, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<EventAnalyticsResponse>(caller.Error);
        }

        var analytics = await _analytics.FindByEventAsync(communityEvent.Id, cancellationToken)
            ?? EventAnalytics.Open(
                EventAnalyticsId.From(communityEvent.Id.Value),
                communityEvent.Id,
                communityEvent.OrganizationId,
                communityEvent.Location.CountryCode,
                communityEvent.CategoryKey,
                communityEvent.Location.NeighborhoodAreaId,
                communityEvent.CreatedAt);

        return Result.Success(CommunityLifeCatalogueMapper.ToResponse(analytics));
    }
}
