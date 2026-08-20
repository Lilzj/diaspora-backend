using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Makes an event discoverable.
/// <para>
/// <b>The spam pass runs here and nowhere else.</b> A draft costs nobody anything; a published event
/// is pushed at members. A severe signal holds the event hidden rather than refusing the publication
/// outright, so the organizer keeps their work and a moderator decides — refusing would make a false
/// positive indistinguishable from a bug, and the organizers most likely to trip a heuristic are
/// small bodies posting their first event.
/// </para>
/// <para>
/// <b>The spam seam is optional and a deployment without it publishes normally.</b> Holding
/// everything because no assessor is registered would make the module unusable rather than safe.
/// </para>
/// </summary>
public sealed class PublishEventCommandHandler
    : ICommandHandler<PublishEventCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeSpamAssessor _spam;
    private readonly CommunityEventReadContext _read;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public PublishEventCommandHandler(
        ICommunityEventRepository events,
        IOrganizationRepository organizations,
        IEventAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeSpamAssessor spam,
        CommunityEventReadContext read,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _events = events;
        _organizations = organizations;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _spam = spam;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EventResponse>> Handle(
        PublishEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(command.EventId), cancellationToken);

        if (communityEvent is null)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.EventNotFound);
        }

        var caller = await _authorization.RequireEventWriteAsync(
            communityEvent, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<EventResponse>(caller.Error);
        }

        var now = _clock.UtcNow;

        if (CommunityLifeSharedMapper.ParseEnum<EventVisibility>(command.Request.Visibility)
            is { } visibility)
        {
            var set = communityEvent.SetVisibility(visibility, baseRevision: null);

            if (set.IsFailure)
            {
                return Result.Failure<EventResponse>(set.Error);
            }
        }

        var published = communityEvent.Publish(now, caller.Value);

        if (published.IsFailure)
        {
            return Result.Failure<EventResponse>(published.Error);
        }

        await AssessAsync(communityEvent, caller.Value, now, cancellationToken);

        var analytics = await _analytics.FindByEventAsync(communityEvent.Id, cancellationToken);

        analytics?.RecordPublication(now);

        _events.Update(communityEvent);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        CommunityLifeTelemetry.EventsPublished.Add(
            1,
            new KeyValuePair<string, object?>("category", communityEvent.CategoryKey),
            new KeyValuePair<string, object?>(
                "country", communityEvent.Location.CountryCode));

        return Result.Success(await _read.ToResponseAsync(communityEvent, cancellationToken));
    }

    /// <summary>
    /// Runs the contributed spam signals and applies the strongest verdict.
    /// <para>
    /// A severe signal hides the event. Two warnings leave it visible and write the reasons into
    /// the moderation trail, so whoever eventually looks at it — because it was reported, or because
    /// they were reviewing the organizer — sees what the heuristics objected to rather than having
    /// to re-derive it. Hiding on a warning would take down the small bodies posting their first
    /// event, who are exactly who a heuristic misfires on.
    /// </para>
    /// </summary>
    private async Task AssessAsync(
        CommunityEvent communityEvent,
        UserId caller,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!_options.Value.EnableSpamHold || !_spam.HasProviders)
        {
            return;
        }

        var live = await _events.CountLiveByOrganizerAsync(
            communityEvent.OrganizerUserId, cancellationToken);

        var verified = communityEvent.OrganizationId is { } organizationId
            && (await _organizations.GetAsync(organizationId, cancellationToken))
                ?.TrustMark.IsVerified == true;

        var assessment = await _spam.AssessAsync(
            communityEvent, live, verified, cancellationToken);

        if (!assessment.HasSignals)
        {
            return;
        }

        CommunityLifeTelemetry.SpamSignalsRaised.Add(assessment.Signals.Count);

        if (assessment.ShouldHold)
        {
            communityEvent.Hide(assessment.Summarise(), moderatorId: null, now);

            return;
        }

        if (assessment.ShouldFlag)
        {
            communityEvent.AddModerationNote(
                _guids.NewSequentialGuid(), caller, assessment.Summarise(), now);
        }
    }
}
