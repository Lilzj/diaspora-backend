using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Extensibility.Views;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// One event, by identifier or by slug.
/// <para>
/// <b>Readability is the authorization service's decision, and a members-only event is refused with
/// a forbidden rather than a not-found.</b> That is the one place in the module where the two
/// differ: the event's existence is not the secret, and somebody told it does not exist would never
/// learn that joining the organization is how they get in.
/// </para>
/// <para>
/// <b>The view is recorded after the response is built, and never fails the read.</b> An event page
/// that returned an error because a counter would not move would go down whenever the analytics did.
/// The organizer's own visits are excluded, or an organizer refreshing while they edit would make
/// their event the most-viewed in the city.
/// </para>
/// </summary>
[SideEffecting(Reason = "Records the view that this read caused, excluding the organizer's own.")]
public sealed class GetEventQueryHandler : IQueryHandler<GetEventQuery, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly ICommunityLifeViewTracker _views;
    private readonly IClock _clock;

    public GetEventQueryHandler(
        ICommunityEventRepository events,
        IEventAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read,
        ICommunityLifeViewTracker views,
        IClock clock)
    {
        _events = events;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _read = read;
        _views = views;
        _clock = clock;
    }

    public async Task<Result<EventResponse>> Handle(
        GetEventQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var communityEvent = await LoadAsync(query, cancellationToken);

        if (communityEvent is null || communityEvent.IsDeleted)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.EventNotFound);
        }

        var readable = await _authorization.RequireEventReadAsync(
            communityEvent, cancellationToken);

        if (readable.IsFailure)
        {
            return Result.Failure<EventResponse>(readable.Error);
        }

        var response = await _read.ToResponseAsync(communityEvent, cancellationToken);

        await RecordViewAsync(communityEvent, cancellationToken);

        return Result.Success(response);
    }

    private async Task<CommunityEvent?> LoadAsync(
        GetEventQuery query, CancellationToken cancellationToken)
    {
        if (query.EventId is { } id && id != Guid.Empty)
        {
            return await _events.GetAsync(CommunityEventId.From(id), cancellationToken);
        }

        return string.IsNullOrWhiteSpace(query.Slug)
            ? null
            : await _events.GetBySlugAsync(query.Slug, cancellationToken);
    }

    private async Task RecordViewAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken)
    {
        if (_authorization.IsStaff
            || communityEvent.OrganizerUserId == _authorization.CallerId)
        {
            return;
        }

        // An anonymous reader is one bucket rather than none: without a key every anonymous visit
        // would count as unique, and the unique-view figure would just be the total.
        var isUnique = _views.RecordView(
            communityEvent.Id.Value,
            _authorization.CallerId?.Value.ToString() ?? "anonymous");

        communityEvent.RecordView(isUnique);

        var analytics = await _analytics.FindByEventAsync(communityEvent.Id, cancellationToken);

        analytics?.RecordView(isUnique, _clock.UtcNow);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
