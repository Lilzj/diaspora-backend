using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Requests;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.Modules.CommunityLife.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// Sets up a repeating event and writes its first dates.
/// <para>
/// <b>The publication decision is made before the expansion, not after, and the order is
/// load-bearing.</b> A published series writes its dates already announced; a draft one writes them
/// as drafts. Expanding first and publishing afterwards would mean walking back over every
/// occurrence to announce it, which is the same work done twice and a window in which a series says
/// it is live while its dates say they are not.
/// </para>
/// <para>
/// <b>The organization is checked, never trusted.</b> An unchecked identifier would let anybody
/// announce a weekly service under a church's name — the same refusal a single event makes, and more
/// consequential here because one request would put it on the calendar every week for six months.
/// </para>
/// </summary>
public sealed class CreateEventSeriesCommandHandler
    : ICommandHandler<CreateEventSeriesCommand, EventSeriesResponse>
{
    private readonly IEventSeriesRepository _series;
    private readonly IOrganizationRepository _organizations;
    private readonly IUnitOfWork _unitOfWork;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly CommunityLifeSlugAllocator _slugs;
    private readonly EventSeriesExpansionService _expansion;
    private readonly EventSeriesReadContext _read;
    private readonly IOptions<CommunityLifeOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateEventSeriesCommandHandler(
        IEventSeriesRepository series,
        IOrganizationRepository organizations,
        IUnitOfWork unitOfWork,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityLifeSlugAllocator slugs,
        EventSeriesExpansionService expansion,
        EventSeriesReadContext read,
        IOptions<CommunityLifeOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _series = series;
        _organizations = organizations;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _catalog = catalog;
        _neighborhoods = neighborhoods;
        _slugs = slugs;
        _expansion = expansion;
        _read = read;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<EventSeriesResponse>> Handle(
        CreateEventSeriesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(caller.Error);
        }

        var request = command.Request;
        var now = _clock.UtcNow;

        var host = await ResolveHostAsync(request, cancellationToken);

        if (host.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(host.Error);
        }

        var live = await _series.CountLiveByOrganizerAsync(caller.Value, cancellationToken);

        if (live >= _options.Value.MaxLiveSeriesPerOrganizer)
        {
            return Result.Failure<EventSeriesResponse>(CommunityLifeErrors.TooManyEvents);
        }

        var category = await _catalog.RequireCategoryAsync(
            request.CategoryKey, cancellationToken: cancellationToken);

        if (category.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(category.Error);
        }

        var built = await BuildAsync(request, cancellationToken);

        if (built.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(built.Error);
        }

        var series = EventSeries.Draft(
            EventSeriesId.From(_guids.NewSequentialGuid()),
            host.Value?.Id,
            caller.Value,
            built.Value.Title,
            await _slugs.ForSeriesAsync(request.Title, cancellationToken),
            built.Value.Description,
            category.Value.Key,
            built.Value.Recurrence,
            built.Value.Schedule,
            built.Value.Location,
            built.Value.Venue,
            built.Value.AttendanceMode,
            now);

        if (series.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(series.Error);
        }

        var applied = ApplyTemplate(
            series.Value, request, built.Value, category.Value.DefaultsToRsvp);

        if (applied.IsFailure)
        {
            return Result.Failure<EventSeriesResponse>(applied.Error);
        }

        // Before the expansion. See the class summary.
        if (request.Publish)
        {
            var published = series.Value.Publish(now, caller.Value);

            if (published.IsFailure)
            {
                return Result.Failure<EventSeriesResponse>(published.Error);
            }

            CommunityLifeTelemetry.SeriesPublished.Add(
                1,
                new KeyValuePair<string, object?>(
                    "frequency", built.Value.Recurrence.Frequency.ToString()));
        }

        foreach (var date in built.Value.Exceptions)
        {
            var excluded = series.Value.AddException(
                date, SeriesExceptionReason.Skipped, now);

            if (excluded.IsFailure)
            {
                return Result.Failure<EventSeriesResponse>(excluded.Error);
            }
        }

        await _series.AddAsync(series.Value, cancellationToken);

        await _expansion.ExpandAsync(series.Value, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await _read.ToResponseAsync(series.Value, cancellationToken));
    }

    /// <summary>The organizing body, where one was named, and whether the caller may run its events.</summary>
    private async Task<Result<Organization?>> ResolveHostAsync(
        CreateEventSeriesRequest request, CancellationToken cancellationToken)
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

    private async Task<Result<SeriesContent>> BuildAsync(
        CreateEventSeriesRequest request, CancellationToken cancellationToken)
    {
        var title = CommunityLifeTitle.Create(request.Title);

        if (title.IsFailure)
        {
            return Result.Failure<SeriesContent>(title.Error);
        }

        var description = CommunityLifeDescription.Create(request.Summary, request.Description);

        if (description.IsFailure)
        {
            return Result.Failure<SeriesContent>(description.Error);
        }

        var recurrence = RecurrenceRuleFactory.Create(request.Recurrence);

        if (recurrence.IsFailure)
        {
            return Result.Failure<SeriesContent>(recurrence.Error);
        }

        var schedule = SeriesSchedule.Create(
            request.StartDate, request.StartTime, request.DurationMinutes, request.TimeZoneId);

        if (schedule.IsFailure)
        {
            return Result.Failure<SeriesContent>(schedule.Error);
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
            return Result.Failure<SeriesContent>(venue.Error);
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
            return Result.Failure<SeriesContent>(audience.Error);
        }

        var contact = ContactDetails.Create(
            request.Contact?.Phone,
            request.Contact?.WhatsApp,
            request.Contact?.Email,
            request.Contact?.Website,
            request.Contact?.ContactName);

        if (contact.IsFailure)
        {
            return Result.Failure<SeriesContent>(contact.Error);
        }

        var location = await BuildLocationAsync(request, cancellationToken);

        return location.IsFailure
            ? Result.Failure<SeriesContent>(location.Error)
            : Result.Success(new SeriesContent(
                title.Value,
                description.Value,
                recurrence.Value,
                schedule.Value,
                location.Value,
                venue.Value,
                audience.Value,
                contact.Value,
                CommunityLifeSharedMapper.ParseEnum<EventAttendanceMode>(request.AttendanceMode)
                    ?? EventAttendanceMode.InPerson,
                request.Recurrence.Exceptions ?? []));
    }

    private async Task<Result<CommunityLifeLocationContext>> BuildLocationAsync(
        CreateEventSeriesRequest request, CancellationToken cancellationToken)
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

    private static Result ApplyTemplate(
        EventSeries series,
        CreateEventSeriesRequest request,
        SeriesContent content,
        bool categoryDefaultsToRsvp)
    {
        var updated = series.UpdateContent(
            baseRevision: null,
            audience: content.Audience,
            contact: content.Contact,
            hostName: request.HostName,
            capacityMaximum: request.Capacity,

            // The catalogue's default where the client said nothing — the same rule a single event
            // follows, so a weekly service and a one-off service behave identically.
            requiresRsvp: request.RequiresRsvp ?? categoryDefaultsToRsvp);

        return updated.IsFailure ? Result.Failure(updated.Error) : Result.Success();
    }

    private sealed record SeriesContent(
        CommunityLifeTitle Title,
        CommunityLifeDescription Description,
        RecurrenceRule Recurrence,
        SeriesSchedule Schedule,
        CommunityLifeLocationContext Location,
        EventVenue Venue,
        EventAudience Audience,
        ContactDetails Contact,
        EventAttendanceMode AttendanceMode,
        IReadOnlyList<DateOnly> Exceptions);
}
