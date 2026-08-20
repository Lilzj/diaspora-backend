using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.Series;

/// <summary>
/// A rule that produces events, and the identity that binds them together.
/// <para>
/// <b>A series is a generator, not a kind of event.</b> Every occurrence it produces is an ordinary
/// <c>CommunityEvent</c> — with its own RSVPs, its own capacity, its own check-in, its own analytics
/// row and its own slug — and that is the single most consequential decision in B12.1. The
/// alternative, a parallel "occurrence" entity, would have meant a second registration model, a
/// second attendance model, a second capacity settlement path and a second analytics store, all
/// duplicating machinery this module already has and already keeps reconcilable. It would also have
/// left every consumer with two contracts for one fact.
/// </para>
/// <para>
/// <b>What lives here is what the occurrences have in common</b>: the rule, the local time of day,
/// the zone, and the template every generated event is stamped from. What does not live here is
/// anything about a particular date — who is coming to it, whether it is full, whether it happened.
/// Those belong to the occurrence, because they differ per occurrence and because a series with
/// three years of weekly meetings must never be a row somebody loads to answer them.
/// </para>
/// <para>
/// <b>Occurrences are materialised to a horizon, not generated on read.</b> A rule that never ends
/// cannot be fully expanded, and a rule expanded at read time produces identifiers that exist only
/// for the length of one response — which is precisely what the frontend said it could not build
/// against, because an RSVP, a reminder and an attendance record all have to name the same
/// occurrence tomorrow. <see cref="MaterialisedThrough"/> is how far the writing has got;
/// the expansion sweep moves it forward.
/// </para>
/// <para>
/// <b>Exceptions are dates, not identifiers.</b> See <see cref="SeriesDateException"/>.
/// </para>
/// </summary>
public sealed class EventSeries
    : AggregateRoot<EventSeriesId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<SeriesDateException> _exceptions = [];

    private EventSeries(
        EventSeriesId id,
        OrganizationId? organizationId,
        UserId organizerUserId,
        CommunityLifeTitle title,
        CommunityLifeSlug slug,
        CommunityLifeDescription description,
        string categoryKey,
        RecurrenceRule recurrence,
        SeriesSchedule schedule,
        CommunityLifeLocationContext location,
        EventVenue venue,
        EventAttendanceMode attendanceMode,
        DateTimeOffset createdAt) : base(id)
    {
        OrganizationId = organizationId;
        OrganizerUserId = organizerUserId;
        Title = title;
        Slug = slug;
        Description = description;
        CategoryKey = categoryKey;
        Recurrence = recurrence;
        Schedule = schedule;
        Location = location;
        Venue = venue;
        AttendanceMode = attendanceMode;
        Audience = EventAudience.Default;
        Contact = ContactDetails.Empty;
        Status = EventSeriesStatus.Draft;
        Visibility = EventVisibility.Private;
        RequiresRsvp = true;
        CreatedAt = createdAt;
    }

    private EventSeries() { }

    // ── Ownership & classification ──────────────────────────────────────────────────────────

    public OrganizationId? OrganizationId { get; private set; }

    public UserId OrganizerUserId { get; private set; } = default!;

    public string? HostName { get; private set; }

    public string CategoryKey { get; private set; } = string.Empty;

    // ── The template every occurrence is stamped from ───────────────────────────────────────

    public CommunityLifeTitle Title { get; private set; } = default!;

    /// <summary>
    /// The series' own handle. Distinct from every occurrence's, which are allocated per date, so a
    /// link to "the Tuesday meetup" and a link to "the Tuesday meetup on 4 August" are different
    /// links and neither can be mistaken for the other.
    /// </summary>
    public CommunityLifeSlug Slug { get; private set; } = default!;

    public CommunityLifeDescription Description { get; private set; } = default!;

    public RecurrenceRule Recurrence { get; private set; } = default!;

    public SeriesSchedule Schedule { get; private set; } = default!;

    public CommunityLifeLocationContext Location { get; private set; } = default!;

    public EventVenue Venue { get; private set; } = default!;

    public EventAttendanceMode AttendanceMode { get; private set; }

    public EventAudience Audience { get; private set; } = default!;

    public ContactDetails Contact { get; private set; } = default!;

    /// <summary>The ceiling stamped onto each occurrence. Null means uncapped.</summary>
    public int? CapacityMaximum { get; private set; }

    public bool RequiresRsvp { get; private set; }

    public bool IsAttendeeListPublic { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public EventSeriesStatus Status { get; private set; }

    public EventVisibility Visibility { get; private set; }

    /// <summary>
    /// The local date occurrences have been written up to, inclusive. Null before any were.
    /// <para>
    /// A local date rather than an instant, because the horizon is a question about the rule's own
    /// calendar and a UTC boundary would cut a Lagos evening off a day early.
    /// </para>
    /// </summary>
    public DateOnly? MaterialisedThrough { get; private set; }

    /// <summary>
    /// The highest ordinal written. What the next expansion resumes from, and the reason the sweep
    /// never has to re-walk the whole rule to find where it left off.
    /// </summary>
    public int LastMaterialisedSequence { get; private set; }

    /// <summary>How many occurrences exist as rows right now.</summary>
    public int OccurrenceCount { get; private set; }

    public int CompletedCount { get; private set; }

    public int CancelledOccurrenceCount { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<SeriesDateException> Exceptions => _exceptions.AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether the sweep should keep writing occurrences for it.</summary>
    public bool GeneratesOccurrences =>
        !IsDeleted
        && EventSeriesStatusTransitions.GeneratesOccurrences(Status)
        && OccurrenceCount < CommunityLifeLimits.MaxSeriesOccurrences
        && (Recurrence.TotalOccurrences is not { } total
            || LastMaterialisedSequence < total);

    /// <summary>Whether an occurrence written now is announced or drafted.</summary>
    public bool PublishesOccurrences =>
        EventSeriesStatusTransitions.PublishesOccurrences(Status);

    /// <summary>The removed dates, as the expander wants them.</summary>
    public IReadOnlySet<DateOnly> ExceptionDates =>
        _exceptions.Select(exception => exception.LocalDate).ToHashSet();

    /// <summary>Whether the series has the minimum somebody needs to decide whether to come.</summary>
    public bool IsPublishable =>
        Description.Summary.Length > 0
        && CategoryKey.Length > 0
        && (AttendanceMode == EventAttendanceMode.Online || Venue.IsAddressable)
        && (AttendanceMode == EventAttendanceMode.InPerson || Venue.HasOnlineAccess);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Drafts a series.
    /// <para>
    /// Always into <see cref="EventSeriesStatus.Draft"/>, for the same reason an event is: publishing
    /// is a separate act with its own permission and its own readiness check, and a create that could
    /// publish would skip both — here across every date the rule produces at once.
    /// </para>
    /// </summary>
    public static Result<EventSeries> Draft(
        EventSeriesId id,
        OrganizationId? organizationId,
        UserId organizerUserId,
        CommunityLifeTitle title,
        CommunityLifeSlug slug,
        CommunityLifeDescription description,
        string? categoryKey,
        RecurrenceRule recurrence,
        SeriesSchedule schedule,
        CommunityLifeLocationContext location,
        EventVenue venue,
        EventAttendanceMode attendanceMode,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(organizerUserId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(recurrence);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(venue);

        var key = Organizations.Organization.NormalizeKey(categoryKey);

        if (key is null)
        {
            return Result.Failure<EventSeries>(CommunityLifeErrors.EventCategoryInvalid);
        }

        var placed = EnsureVenueMatchesMode(venue, attendanceMode);

        if (placed.IsFailure)
        {
            return Result.Failure<EventSeries>(placed.Error);
        }

        // A rule that produces nothing is a rule the organizer got wrong — the 31st of February,
        // an until-date before the start. Refused at creation rather than filed as a series that
        // will never generate anything and will never say why.
        var first = RecurrenceExpander.Expand(recurrence, schedule, maxSlots: 1);

        if (first.Count == 0)
        {
            return Result.Failure<EventSeries>(CommunityLifeErrors.RecurrenceProducesNothing);
        }

        var series = new EventSeries(
            id,
            organizationId,
            organizerUserId,
            title,
            slug,
            description,
            key,
            recurrence,
            schedule,
            location,
            venue,
            attendanceMode,
            createdAt);

        series.RaiseDomainEvent(new EventSeriesCreatedDomainEvent
        {
            EventSeriesId = id,
            OrganizationId = organizationId,
            CreatedBy = organizerUserId,
            Title = title.Value,
            Slug = slug.Value,
            CategoryKey = key,
            CountryCode = location.CountryCode,
            NeighborhoodAreaId = location.NeighborhoodAreaId,
            RecurrenceDescription = recurrence.Describe(schedule.FirstLocalDate),
            FirstStartsAt = first[0].StartsAt
        });

        return Result.Success(series);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Template
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the descriptive template. Null means "leave it alone", as everywhere else in B12.
    /// </summary>
    public Result<IReadOnlyList<string>> UpdateContent(
        int? baseRevision,
        CommunityLifeTitle? title = null,
        CommunityLifeDescription? description = null,
        EventAudience? audience = null,
        ContactDetails? contact = null,
        string? hostName = null,
        int? capacityMaximum = null,
        bool? requiresRsvp = null,
        bool? isAttendeeListPublic = null)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(guard.Error);
        }

        var changed = new List<string>();

        if (title is not null && title != Title)
        {
            Title = title;
            changed.Add(nameof(Title));
        }

        if (description is not null && description != Description)
        {
            Description = description;
            changed.Add(nameof(Description));
        }

        if (audience is not null && audience != Audience)
        {
            Audience = audience;
            changed.Add(nameof(Audience));
        }

        if (contact is not null && contact != Contact)
        {
            Contact = contact;
            changed.Add(nameof(Contact));
        }

        if (hostName is not null)
        {
            var trimmed = Clip(hostName, CommunityLifeLimits.MaxDisplayName);

            if (!string.Equals(trimmed, HostName, StringComparison.Ordinal))
            {
                HostName = trimmed;
                changed.Add(nameof(HostName));
            }
        }

        if (capacityMaximum is { } maximum)
        {
            if (maximum < 1)
            {
                return Result.Failure<IReadOnlyList<string>>(
                    CommunityLifeErrors.CapacityInvalid);
            }

            if (maximum != CapacityMaximum)
            {
                CapacityMaximum = maximum;
                changed.Add(nameof(CapacityMaximum));
            }
        }

        if (requiresRsvp is { } takesRsvp && takesRsvp != RequiresRsvp)
        {
            RequiresRsvp = takesRsvp;
            changed.Add(nameof(RequiresRsvp));
        }

        if (isAttendeeListPublic is { } listPublic && listPublic != IsAttendeeListPublic)
        {
            IsAttendeeListPublic = listPublic;
            changed.Add(nameof(IsAttendeeListPublic));
        }

        if (changed.Count > 0)
        {
            Touch();
        }

        return Result.Success<IReadOnlyList<string>>(changed);
    }

    /// <summary>Moves the whole series in space.</summary>
    public Result<IReadOnlyList<string>> Relocate(
        CommunityLifeLocationContext location,
        EventVenue venue,
        EventAttendanceMode attendanceMode,
        int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(venue);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(guard.Error);
        }

        var placed = EnsureVenueMatchesMode(venue, attendanceMode);

        if (placed.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(placed.Error);
        }

        var changed = new List<string>();

        if (location != Location)
        {
            Location = location;
            changed.Add(nameof(Location));
        }

        if (venue != Venue)
        {
            Venue = venue;
            changed.Add(nameof(Venue));
        }

        if (attendanceMode != AttendanceMode)
        {
            AttendanceMode = attendanceMode;
            changed.Add(nameof(AttendanceMode));
        }

        if (changed.Count > 0)
        {
            Touch();
        }

        return Result.Success<IReadOnlyList<string>>(changed);
    }

    /// <summary>
    /// Restates the rule or the local time.
    /// <para>
    /// <b>Refused when it would produce nothing</b>, and refused rather than accepted-and-silent,
    /// because a series that has stopped generating looks identical to one whose horizon has not
    /// caught up yet.
    /// </para>
    /// <para>
    /// The horizon is rewound to the point the edit takes effect from, so the sweep rewrites what
    /// the new rule now says rather than leaving the old rule's dates standing.
    /// </para>
    /// </summary>
    public Result<IReadOnlyList<string>> Rebase(
        RecurrenceRule? recurrence,
        SeriesSchedule? schedule,
        int? fromSequence,
        int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(guard.Error);
        }

        var nextRule = recurrence ?? Recurrence;
        var nextSchedule = schedule ?? Schedule;

        if (nextRule == Recurrence && nextSchedule == Schedule)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        if (RecurrenceExpander.Expand(nextRule, nextSchedule, maxSlots: 1).Count == 0)
        {
            return Result.Failure<IReadOnlyList<string>>(
                CommunityLifeErrors.RecurrenceProducesNothing);
        }

        var changed = new List<string>();

        if (nextRule != Recurrence)
        {
            Recurrence = nextRule;
            changed.Add(nameof(Recurrence));
        }

        if (nextSchedule != Schedule)
        {
            Schedule = nextSchedule;
            changed.Add(nameof(Schedule));
        }

        // Rewinding to the sequence before the edit's first affected occurrence is what makes the
        // sweep rewrite from there. Rewinding to zero would try to regenerate occurrences that
        // already happened.
        LastMaterialisedSequence = Math.Max(0, (fromSequence ?? 1) - 1);
        MaterialisedThrough = null;

        Touch();

        return Result.Success<IReadOnlyList<string>>(changed);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result Publish(DateTimeOffset now, UserId? publishedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.EventSeriesNotFound);
        }

        if (Status == EventSeriesStatus.Published)
        {
            return Result.Failure(CommunityLifeErrors.EventAlreadyPublished);
        }

        if (!IsPublishable)
        {
            return Result.Failure(CommunityLifeErrors.EventNotPublishable);
        }

        var moved = MoveTo(EventSeriesStatus.Published, now, publishedBy, reason: null);

        if (moved.IsFailure)
        {
            return moved;
        }

        PublishedAt = now;
        FirstPublishedAt ??= now;
        Visibility = Visibility == EventVisibility.Private
            ? EventVisibility.Public
            : Visibility;

        return Result.Success();
    }

    /// <summary>Suspends generation without telling anybody an announced date is off.</summary>
    public Result Pause(DateTimeOffset now, UserId? changedBy, string? reason) =>
        MoveTo(EventSeriesStatus.Paused, now, changedBy, reason);

    public Result Resume(DateTimeOffset now, UserId? changedBy) =>
        MoveTo(EventSeriesStatus.Published, now, changedBy, reason: null);

    /// <summary>The rule has run out. Set by the expansion sweep, not by an organizer.</summary>
    public Result Complete(DateTimeOffset now) =>
        MoveTo(EventSeriesStatus.Completed, now);

    /// <summary>
    /// Calls the whole series off.
    /// <para>
    /// The occurrences are cancelled by the handler, one at a time and each raising its own
    /// cancellation, because each has its own guest list to release and its own people to tell. The
    /// series records only that it is over.
    /// </para>
    /// </summary>
    public Result Cancel(DateTimeOffset now, UserId? cancelledBy, string? reason)
    {
        var moved = MoveTo(EventSeriesStatus.Cancelled, now, cancelledBy, reason);

        if (moved.IsFailure)
        {
            return moved;
        }

        CancelledAt = now;
        CancellationReason = Clip(reason, CommunityLifeLimits.MaxNoteLength);

        return Result.Success();
    }

    public Result MoveTo(
        EventSeriesStatus target,
        DateTimeOffset now,
        UserId? changedBy = null,
        string? reason = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.EventSeriesNotFound);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        if (!EventSeriesStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(CommunityLifeErrors.EventSeriesTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        ModifiedAt = now;

        if (target is EventSeriesStatus.Cancelled)
        {
            Visibility = EventVisibility.Private;
        }

        RaiseDomainEvent(new EventSeriesStatusChangedDomainEvent
        {
            EventSeriesId = Id,
            OrganizationId = OrganizationId,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    public Result SetVisibility(EventVisibility visibility, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (visibility == EventVisibility.MembersOnly && OrganizationId is null)
        {
            return Result.Failure(CommunityLifeErrors.EventNotMemberVisible);
        }

        if (Visibility == visibility)
        {
            return Result.Success();
        }

        Visibility = visibility;
        Touch();

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Materialisation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records that occurrences have been written up to a point.
    /// <para>
    /// <b>Deliberately does not bump the revision.</b> The sweep extending a horizon while an
    /// organizer edits the series must not make the organizer's save fail — the same reasoning the
    /// event aggregate applies to attendance settlement and view counting.
    /// </para>
    /// </summary>
    public void RecordMaterialisation(
        int lastSequence, DateOnly through, int written, DateTimeOffset now)
    {
        LastMaterialisedSequence = Math.Max(LastMaterialisedSequence, lastSequence);

        MaterialisedThrough = MaterialisedThrough is { } current && current > through
            ? current
            : through;

        OccurrenceCount = Math.Max(0, OccurrenceCount + Math.Max(0, written));
        ModifiedAt = now;
    }

    /// <summary>An occurrence finished. Counted here so a series card renders without a scan.</summary>
    public void RecordOccurrenceCompleted(DateTimeOffset now)
    {
        CompletedCount++;
        ModifiedAt = now;
    }

    /// <summary>An occurrence was called off.</summary>
    public void RecordOccurrenceCancelled(DateTimeOffset now)
    {
        CancelledOccurrenceCount++;
        ModifiedAt = now;
    }

    /// <summary>
    /// Removes a date from the rule.
    /// <para>
    /// Idempotent on the date, because the two callers — a cancellation and a move — can both reach
    /// it for the same slot, and a second exception on one date would make the count meaningless.
    /// </para>
    /// </summary>
    public Result AddException(DateOnly localDate, string reason, DateTimeOffset now)
    {
        if (_exceptions.Any(exception => exception.LocalDate == localDate))
        {
            return Result.Success();
        }

        if (_exceptions.Count >= CommunityLifeLimits.MaxSeriesExceptions)
        {
            return Result.Failure(CommunityLifeErrors.SeriesExceptionLimit);
        }

        _exceptions.Add(SeriesDateException.Create(localDate, reason, now));
        ModifiedAt = now;

        return Result.Success();
    }

    /// <summary>Puts a removed date back. The undo for a skip that was a mistake.</summary>
    public void RemoveException(DateOnly localDate, DateTimeOffset now)
    {
        var removed = _exceptions.RemoveAll(exception => exception.LocalDate == localDate);

        if (removed > 0)
        {
            ModifiedAt = now;
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(CommunityLifeErrors.RevisionConflict)
            : Result.Success();

    /// <summary>Raises the update event. Called by the handler once, after every field is settled.</summary>
    public void RaiseUpdated(
        SeriesEditScope scope,
        IReadOnlyList<string> changedFields,
        UserId? changedBy,
        bool isMaterialChange,
        int affectedOccurrenceCount)
    {
        ArgumentNullException.ThrowIfNull(changedFields);

        if (changedFields.Count == 0)
        {
            return;
        }

        RaiseDomainEvent(new EventSeriesUpdatedDomainEvent
        {
            EventSeriesId = Id,
            OrganizationId = OrganizationId,
            ChangedBy = changedBy,
            Scope = scope,
            ChangedFields = changedFields,
            IsMaterialChange = isMaterialChange,
            AffectedOccurrenceCount = affectedOccurrenceCount
        });
    }

    private static Result EnsureVenueMatchesMode(EventVenue venue, EventAttendanceMode mode)
    {
        if (mode is EventAttendanceMode.InPerson or EventAttendanceMode.Hybrid
            && !venue.IsAddressable)
        {
            return Result.Failure(CommunityLifeErrors.VenueRequired);
        }

        return mode == EventAttendanceMode.Online && venue.IsAddressable
            ? Result.Failure(CommunityLifeErrors.VenueNotPermitted)
            : Result.Success();
    }

    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.EventSeriesNotFound);
        }

        return EventSeriesStatusTransitions.IsEditable(Status)
            ? EnsureRevision(baseRevision)
            : Result.Failure(CommunityLifeErrors.EventSeriesTerminal);
    }

    private void Touch() => Revision++;

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > max ? trimmed[..max] : trimmed;
    }
}
