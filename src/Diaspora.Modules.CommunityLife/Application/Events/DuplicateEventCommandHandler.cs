using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>
/// Copies an event into a new draft.
/// <para>
/// <b>Copied:</b> title (prefixed), description, category, venue, location, attendance mode,
/// capacity, audience and contact details — everything about how the event runs, which for a repeat
/// is everything except when.
/// </para>
/// <para>
/// <b>Not copied:</b> publication state (always a draft), the deep-link handle, RSVPs, attendance,
/// media, series membership, cancellation state and the analytics rollup — the copy opens its own at
/// zero. Carrying RSVPs over would tell forty people they are attending something they never agreed
/// to, and would send them reminders for it.
/// </para>
///
/// <para>
/// ── <b>The schedule, which is the whole decision</b> ──
/// </para>
/// <para>
/// An event's dates are the one field a copy cannot simply inherit: the source is in the past, and
/// <c>EventSchedule</c> refuses a start that has already happened. So the copy needs a new date, and
/// the only question is which.
/// </para>
/// <para>
/// <b>Whole weeks forward, preserving weekday and time of day.</b> Not "a week from today", not
/// "tomorrow" — a community event that repeats repeats on its weekday. A Saturday-morning language
/// exchange duplicated on a Tuesday should land on the next Saturday morning, not on Tuesday. The
/// duration, the registration deadline's lead time and the check-in offset are all preserved by
/// shifting every timestamp by the same amount.
/// </para>
/// <para>
/// The organiser can say otherwise: <c>StartsAt</c> on the command overrides it, and the same shift
/// is applied from whatever they chose. The default is a sensible starting point for a form they are
/// about to edit, not a claim about when the event will be — which is why the copy is a draft.
/// </para>
/// </summary>
public sealed class DuplicateEventCommandHandler
    : ICommandHandler<DuplicateEventCommand, EventResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly IEventAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeSlugAllocator _slugs;
    private readonly CommunityEventReadContext _read;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public DuplicateEventCommandHandler(
        ICommunityEventRepository events,
        IEventAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeSlugAllocator slugs,
        CommunityEventReadContext read,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _events = events;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _catalog = catalog;
        _slugs = slugs;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EventResponse>> Handle(
        DuplicateEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<EventResponse>(caller.Error);
        }

        var source = await _events.GetAsync(
            CommunityEventId.From(command.EventId), cancellationToken);

        if (source is null)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.EventNotFound);
        }

        // The organiser, not merely a member of the hosting organisation. Copying somebody else's
        // event would author it under the copier's name with the original's details intact.
        if (source.OrganizerUserId != caller.Value)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.NotEventOrganizer);
        }

        var live = await _events.CountLiveByOrganizerAsync(caller.Value, cancellationToken);

        if (live >= _options.Value.MaxLiveEventsPerOrganizer)
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.TooManyEvents);
        }

        var now = _clock.UtcNow;

        var schedule = ShiftForward(source.Schedule, command.StartsAt, now);

        if (schedule.IsFailure)
        {
            return Result.Failure<EventResponse>(schedule.Error);
        }

        var titleText = string.IsNullOrWhiteSpace(command.Title)
            ? Copied(source.Title.Value)
            : command.Title;

        // Same title on the same date is a double-submit rather than a second event, which is what
        // the create path decides too. A different date makes it a repeat, and that is allowed.
        if (await _events.HasDuplicateAsync(
                caller.Value, titleText, schedule.Value.StartsAt, excluding: null, cancellationToken))
        {
            return Result.Failure<EventResponse>(CommunityLifeErrors.DuplicateEvent);
        }

        var title = CommunityLifeTitle.Create(titleText);

        if (title.IsFailure)
        {
            return Result.Failure<EventResponse>(title.Error);
        }

        var category = await _catalog.RequireCategoryAsync(
            source.CategoryKey, cancellationToken: cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<EventResponse>(category.Error);
        }

        // ── Every value object is rebuilt. See DuplicateListingCommandHandler ──
        //
        // `Description`, `Location` and `Venue` are `OwnsOne` — owned entities
        // with an identifying foreign key back to their event. Sharing a source's
        // tracked instance with a second aggregate makes EF try to re-parent it,
        // which it refuses. The schedule is already built fresh above, for a
        // different reason.
        var description = CommunityLifeDescription.Create(
            source.Description.Summary, source.Description.Body);

        if (description.IsFailure)
        {
            return Result.Failure<EventResponse>(description.Error);
        }

        var location = CommunityLifeLocationContext.Create(
            source.Location.CountryCode, source.Location.RegionName, source.Location.CityName);

        if (location.IsFailure)
        {
            return Result.Failure<EventResponse>(location.Error);
        }

        // The neighbourhood is carried through the mutator rather than the
        // factory, which takes only the three place names.
        var placed = source.Location.NeighborhoodAreaId is { } areaId
            ? location.Value.WithNeighborhood(
                areaId, source.Location.AreaPath, source.Location.NeighborhoodName)
            : location.Value;

        var venue = EventVenue.Create(
            source.Venue.Name, source.Venue.AddressLine1, source.Venue.AddressLine2,
            source.Venue.Landmark, source.Venue.PostalCode,
            source.Venue.Latitude, source.Venue.Longitude, source.Venue.OnlineUrl);

        if (venue.IsFailure)
        {
            return Result.Failure<EventResponse>(venue.Error);
        }

        var copy = CommunityEvent.Draft(
            CommunityEventId.From(_guids.NewSequentialGuid()),
            source.OrganizationId,
            caller.Value,
            title.Value,
            await _slugs.ForEventAsync(titleText, schedule.Value.StartsAt, cancellationToken),
            description.Value,
            category.Value.Key,
            schedule.Value,
            placed,
            venue.Value,
            source.AttendanceMode,
            now);

        if (copy.IsFailure)
        {
            return Result.Failure<EventResponse>(copy.Error);
        }

        if (source.Capacity.Maximum is { } maximum)
        {
            copy.Value.SetCapacity(maximum, baseRevision: null);
        }

        await _events.AddAsync(copy.Value, cancellationToken);

        await _analytics.AddAsync(
            EventAnalytics.Open(
                EventAnalyticsId.From(_guids.NewSequentialGuid()),
                copy.Value.Id,
                source.OrganizationId,
                source.Location.CountryCode,
                category.Value.Key,
                source.Location.NeighborhoodAreaId,
                now),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(copy.Value, cancellationToken));
    }

    /// <summary>
    /// Moves the whole schedule forward by a multiple of seven days, so the copy keeps the source's
    /// weekday and time of day. See the class note for why that rather than a fixed offset.
    /// <para>
    /// Every timestamp shifts by the same amount, so the event's duration, its registration lead
    /// time and its check-in offset are all preserved rather than recomputed.
    /// </para>
    /// </summary>
    private static Result<EventSchedule> ShiftForward(
        EventSchedule source, DateTimeOffset? requestedStart, DateTimeOffset now)
    {
        var shift = requestedStart is { } chosen
            ? chosen - source.StartsAt
            : WholeWeeksUntilFuture(source.StartsAt, now);

        return EventSchedule.Create(
            source.StartsAt + shift,
            source.EndsAt + shift,
            source.TimeZoneId,
            source.RegistrationDeadline + shift,
            source.CheckInOpensAt + shift,
            now);
    }

    private static TimeSpan WholeWeeksUntilFuture(DateTimeOffset start, DateTimeOffset now)
    {
        if (start > now)
        {
            // Already in the future — a draft that was never run. Nothing to move.
            return TimeSpan.Zero;
        }

        var weeks = (int)Math.Ceiling((now - start).TotalDays / 7d);

        // Ceiling can land exactly on `now` when the gap is a whole number of weeks; one more week
        // keeps the copy strictly in the future, which is what the schedule requires.
        var shifted = start.AddDays(weeks * 7d);

        return shifted > now
            ? TimeSpan.FromDays(weeks * 7d)
            : TimeSpan.FromDays((weeks + 1) * 7d);
    }

    /// <summary>Prefixes once. See DuplicateGuideCommandHandler for why.</summary>
    private static string Copied(string title) =>
        title.StartsWith("Copy of ", StringComparison.OrdinalIgnoreCase)
            ? title
            : $"Copy of {title}";
}
