using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Drafts an event.
/// <para>
/// <b>The organization is optional, and that is the module's central shaping decision.</b> Most
/// diaspora events are one member inviting their neighbours to a naming ceremony or a fundraiser;
/// requiring an institution behind every one would lose exactly the events that make the platform
/// worth opening. Where a body <em>is</em> named, the caller has to be allowed to run its events.
/// </para>
/// <para>
/// <b>Whether a category needs responses is the catalogue's default, not the client's guess.</b> A
/// Sunday service does not take an RSVP and a workshop with twenty seats does; a client that had to
/// know which would get it wrong for every category added after it shipped.
/// </para>
/// <para>
/// <b>Always lands as a private draft.</b> Publishing is a separate act with its own permission and
/// its own spam pass — a create that could publish would skip both.
/// </para>
/// </summary>
public sealed class CreateEventCommandHandler
    : ICommandHandler<CreateEventCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly CommunityLifeProviderResolver _providers;
    private readonly CommunityLifeGuideResolver _guides;
    private readonly CommunityLifeSlugAllocator _slugs;
    private readonly CommunityLifeMediaClaimer _media;
    private readonly CommunityEventReadContext _read;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateEventCommandHandler(
        ICommunityEventRepository events,
        IOrganizationRepository organizations,
        IEventAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityLifeProviderResolver providers,
        CommunityLifeGuideResolver guides,
        CommunityLifeSlugAllocator slugs,
        CommunityLifeMediaClaimer media,
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
        _catalog = catalog;
        _neighborhoods = neighborhoods;
        _providers = providers;
        _guides = guides;
        _slugs = slugs;
        _media = media;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EventResponse>> Handle(
        CreateEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<EventResponse>(caller.Error);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var host = await ResolveHostAsync(request, cancellationToken);

        if (host.IsFailure)
        {
            return Result.Failure<EventResponse>(host.Error);
        }

        var guard = await GuardAsync(caller.Value, request, cancellationToken);

        if (guard.IsFailure)
        {
            return Result.Failure<EventResponse>(guard.Error);
        }

        var category = await _catalog.RequireCategoryAsync(
            request.CategoryKey, cancellationToken: cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<EventResponse>(category.Error);
        }

        var content = BuildContent(request, now);

        if (content.IsFailure)
        {
            return Result.Failure<EventResponse>(content.Error);
        }

        var location = await BuildLocationAsync(request, cancellationToken);

        if (location.IsFailure)
        {
            return Result.Failure<EventResponse>(location.Error);
        }

        var communityEvent = CommunityEvent.Draft(
            CommunityEventId.From(_guids.NewSequentialGuid()),
            host.Value?.Id,
            caller.Value,
            content.Value.Title,
            await _slugs.ForEventAsync(request.Title, request.StartsAt, cancellationToken),
            content.Value.Description,
            category.Value.Key,
            content.Value.Schedule,
            location.Value,
            content.Value.Venue,
            content.Value.AttendanceMode,
            now);

        if (communityEvent.IsFailure)
        {
            return Result.Failure<EventResponse>(communityEvent.Error);
        }

        var applied = await ApplyDetailAsync(
            communityEvent.Value,
            request,
            content.Value,
            category.Value.DefaultsToRsvp,
            caller.Value,
            cancellationToken);

        if (applied.IsFailure)
        {
            return Result.Failure<EventResponse>(applied.Error);
        }

        await _events.AddAsync(communityEvent.Value, cancellationToken);

        await _analytics.AddAsync(
            EventAnalytics.Open(
                EventAnalyticsId.From(_guids.NewSequentialGuid()),
                communityEvent.Value.Id,
                host.Value?.Id,
                location.Value.CountryCode,
                category.Value.Key,
                location.Value.NeighborhoodAreaId,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await ClaimMediaAsync(communityEvent.Value, request, cancellationToken);

        return Result.Success(
            await _read.ToResponseAsync(communityEvent.Value, cancellationToken));
    }

    /// <summary>
    /// The organizing body, where one was named, and whether the caller may run its events.
    /// <para>
    /// Checked rather than trusted: an unchecked identifier would let anybody publish an event under
    /// a church's name, which is the one thing a member seeing that name relies on.
    /// </para>
    /// </summary>
    private async Task<Result<Organization?>> ResolveHostAsync(
        CreateEventRequest request, CancellationToken cancellationToken)
    {
        if (request.OrganizationId is not { } id || id == Guid.Empty)
        {
            return Result.Success<Organization?>(null);
        }

        var organization = await _organizations.GetAsync(
            OrganizationId.From(id), cancellationToken);

        if (organization is null)
        {
            return Result.Failure<Organization?>(CommunityLifeErrors.OrganizationNotFound);
        }

        if (!organization.AcceptsEvents)
        {
            return Result.Failure<Organization?>(CommunityLifeErrors.OrganizationSuspended);
        }

        var permitted = await _authorization.RequireRoleAsync(
            organization.Id, MembershipRole.Organizer, cancellationToken);

        return permitted.IsFailure
            ? Result.Failure<Organization?>(permitted.Error)
            : Result.Success<Organization?>(organization);
    }

    /// <summary>
    /// The two refusals that are about the organizer rather than the event.
    /// <para>
    /// The duplicate check is exact title and start within one organizer, which catches the
    /// double-submit that a slow network produces. Anything looser belongs to the spam seam, where a
    /// false positive is a moderator's note rather than a refusal — a weekly service genuinely does
    /// repeat.
    /// </para>
    /// </summary>
    private async Task<Result> GuardAsync(
        UserId caller, CreateEventRequest request, CancellationToken cancellationToken)
    {
        var live = await _events.CountLiveByOrganizerAsync(caller, cancellationToken);

        if (live >= _options.Value.MaxLiveEventsPerOrganizer)
        {
            return Result.Failure(CommunityLifeErrors.TooManyEvents);
        }

        return await _events.HasDuplicateAsync(
            caller, request.Title, request.StartsAt, excluding: null, cancellationToken)
            ? Result.Failure(CommunityLifeErrors.DuplicateEvent)
            : Result.Success();
    }

    private static Result<EventContent> BuildContent(
        CreateEventRequest request, DateTimeOffset now)
    {
        var title = CommunityLifeTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<EventContent>(title.Error);
        }

        var description = CommunityLifeDescription.Create(request.Summary, request.Description);

        if (description.IsFailure)
        {
            return Result.Failure<EventContent>(description.Error);
        }

        // The clock is passed in so an event in the past is refused at creation rather than at
        // publication, when the organizer has already done the work.
        var schedule = EventSchedule.Create(
            request.StartsAt,
            request.EndsAt,
            request.TimeZoneId,
            request.RegistrationDeadline,
            request.CheckInOpensAt,
            now);

        if (schedule.IsFailure)
        {
            return Result.Failure<EventContent>(schedule.Error);
        }

        var venue = EventVenue.Create(
            request.Venue?.Name,
            request.Venue?.AddressLine1,
            request.Venue?.AddressLine2,
            request.Venue?.Landmark,
            request.Venue?.PostalCode,
            request.Venue?.Latitude,
            request.Venue?.Longitude,
            request.Venue?.OnlineUrl);

        if (venue.IsFailure)
        {
            return Result.Failure<EventContent>(venue.Error);
        }

        var audience = EventAudience.Create(
            request.IsFamilyFriendly,
            request.IsChildFriendly,
            request.IsStudentFocused,
            request.IsFree,
            request.DressCode,
            request.AccessibilityNotes);

        if (audience.IsFailure)
        {
            return Result.Failure<EventContent>(audience.Error);
        }

        var contact = ContactDetails.Create(
            request.Contact?.Phone,
            request.Contact?.WhatsApp,
            request.Contact?.Email,
            request.Contact?.Website,
            request.Contact?.ContactName);

        return contact.IsFailure
            ? Result.Failure<EventContent>(contact.Error)
            : Result.Success(new EventContent(
                title.Value,
                description.Value,
                schedule.Value,
                venue.Value,
                audience.Value,
                contact.Value,
                CommunityLifeSharedMapper.ParseEnum<EventAttendanceMode>(request.AttendanceMode)
                    ?? EventAttendanceMode.InPerson));
    }

    private async Task<Result<CommunityLifeLocationContext>> BuildLocationAsync(
        CreateEventRequest request, CancellationToken cancellationToken)
    {
        var context = CommunityLifeLocationContext.Create(
            request.CountryCode, request.RegionName, request.CityName);

        if (context.IsFailure)
        {
            return context;
        }

        var resolved = await _neighborhoods.ResolveAsync(
            context.Value, request.NeighborhoodAreaId, cancellationToken);

        if (resolved.IsFailure)
        {
            return resolved;
        }

        return !await _neighborhoods.CountryExistsAsync(request.CountryCode, cancellationToken)
            ? Result.Failure<CommunityLifeLocationContext>(CommunityLifeErrors.CountryInvalid)
            : resolved;
    }

    private async Task<Result> ApplyDetailAsync(
        CommunityEvent communityEvent,
        CreateEventRequest request,
        EventContent content,
        bool categoryDefaultsToRsvp,
        UserId caller,
        CancellationToken cancellationToken)
    {
        var updated = communityEvent.UpdateContent(
            baseRevision: null,
            caller,
            audience: content.Audience,
            contact: content.Contact,
            hostName: request.HostName,

            // The catalogue's default where the client said nothing. See the class summary.
            requiresRsvp: request.RequiresRsvp ?? categoryDefaultsToRsvp);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        if (request.Capacity is { } capacity)
        {
            var set = communityEvent.SetCapacity(capacity, baseRevision: null);

            if (set.IsFailure)
            {
                return set;
            }
        }

        var attached = AttachMedia(communityEvent, request);

        if (attached.IsFailure)
        {
            return attached;
        }

        var guides = await _guides.ForEventAsync(request.GuideIds, cancellationToken);

        if (guides.Count > 0)
        {
            var set = communityEvent.SetGuideReferences(guides);

            if (set.IsFailure)
            {
                return set;
            }
        }

        var providers = await _providers.ResolveAsync(request.ProviderIds, cancellationToken);

        return providers.Count == 0
            ? Result.Success()
            : communityEvent.SetProviderReferences(providers);
    }

    private Result AttachMedia(CommunityEvent communityEvent, CreateEventRequest request)
    {
        if (request.Media is not { Count: > 0 })
        {
            return Result.Success();
        }

        var now = _clock.UtcNow;

        foreach (var media in request.Media)
        {
            var role = CommunityLifeSharedMapper.ParseEnum<CommunityLifeMediaRole>(media.Role)
                ?? CommunityLifeMediaRole.Gallery;

            var attached = communityEvent.AttachMedia(
                media.MediaId, role, media.Caption, now);

            if (attached.IsFailure)
            {
                return attached;
            }
        }

        return Result.Success();
    }

    private async Task ClaimMediaAsync(
        CommunityEvent communityEvent,
        CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Media is not { Count: > 0 })
        {
            return;
        }

        await _media.ClaimManyForEventAsync(
            communityEvent.Id.Value,
            [.. request.Media.Select(media => (media.MediaId, media.Role))],
            cancellationToken);
    }

    private sealed record EventContent(
        CommunityLifeTitle Title,
        CommunityLifeDescription Description,
        EventSchedule Schedule,
        EventVenue Venue,
        EventAudience Audience,
        ContactDetails Contact,
        EventAttendanceMode AttendanceMode);
}
