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

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// An occasion people come to.
/// <para>
/// <b>Bounded in time, which is what makes it a different thing from the organization that runs
/// it.</b> An event has a start, an end, a capacity and a guest list; an organization has none of
/// those and outlives every event it holds. The two are related by an optional reference —
/// <see cref="OrganizationId"/> — and it is optional because a great many of the events on this
/// platform are one member inviting their neighbours to a naming ceremony, with no institution
/// behind them at all. Requiring an organization would either lose those events or force every
/// member to invent a fake one.
/// </para>
/// <para>
/// <b>What it owns:</b> its own content, its schedule, its venue, its agenda, its speakers, its
/// media references, its guide and provider references, its linked resources and its moderation
/// trail. <b>What it points at:</b> an organizing organization, an event category by catalogue key,
/// a B4 administrative area, B5 media identifiers, B7 providers, B8 guides, and — opaquely — B9 and
/// B10 identifiers it never resolves.
/// </para>
/// <para>
/// <b>Attendees are not here.</b> A festival with four thousand RSVPs would load four thousand rows
/// to render its title, and one member changing their mind would take a write lock on the whole
/// event. RSVPs are their own aggregate keyed by event, and the counts on
/// <see cref="Capacity"/> are settled from exactly one place — <see cref="SettleAttendance"/>,
/// called only from the RSVP status handler. That single settlement path is what makes the numbers
/// reconcilable, and it is the same discipline B11 applies to housing units, for the same reason.
/// </para>
/// <para>
/// <b>Concurrency is optimistic and explicit through <see cref="Revision"/>.</b> Two organizers
/// from one committee genuinely do edit one event from two phones on the morning of it.
/// </para>
/// </summary>
public sealed class CommunityEvent
    : AggregateRoot<CommunityEventId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<EventAgendaItem> _agendaItems = [];
    private readonly List<EventSpeaker> _speakers = [];
    private readonly List<EventMediaReference> _mediaReferences = [];
    private readonly List<EventGuideReference> _guideReferences = [];
    private readonly List<EventProviderReference> _providerReferences = [];
    private readonly List<EventLinkedResource> _linkedResources = [];
    private readonly List<EventModerationNote> _moderationNotes = [];

    private CommunityEvent(
        CommunityEventId id,
        OrganizationId? organizationId,
        UserId organizerUserId,
        CommunityLifeTitle title,
        CommunityLifeSlug slug,
        CommunityLifeDescription description,
        string categoryKey,
        EventSchedule schedule,
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
        Schedule = schedule;
        Location = location;
        Venue = venue;
        AttendanceMode = attendanceMode;
        Capacity = EventCapacity.Unlimited;
        Audience = EventAudience.Default;
        Contact = ContactDetails.Empty;
        Engagement = EventEngagement.Empty;
        Status = EventStatus.Draft;
        Visibility = EventVisibility.Private;
        ModerationState = CommunityLifeModerationState.Clear;
        RequiresRsvp = true;
        CreatedAt = createdAt;
    }

    private CommunityEvent() { }

    // ── Ownership & classification ──────────────────────────────────────────────────────────

    /// <summary>
    /// The organization running it, when there is one.
    /// <para>
    /// Optional by design. Most events on a diaspora platform are a member inviting their
    /// neighbours, and requiring an institution would either lose those or force people to invent
    /// one.
    /// </para>
    /// </summary>
    public OrganizationId? OrganizationId { get; private set; }

    /// <summary>
    /// The Community Programme community the host posted this event into, when they chose one —
    /// 21 Oct 2026, "Post in community".
    /// <para>
    /// A plain identifier rather than a typed one, because the community belongs to a module that
    /// composes after this one. Set once, at creation, and only after Community Programme said the
    /// host may post there (<c>ICommunityEventPostingAuthority</c>); nothing here re-checks it, and
    /// nothing moves an event between communities. Distinct from <see cref="OrganizationId"/>: an
    /// organisation runs an event, a community is where it is shown.
    /// </para>
    /// </summary>
    public Guid? CommunityId { get; private set; }

    /// <summary>
    /// The member who runs it. Always set, even when an organization is behind it: somebody has to
    /// answer the phone, and an audit trail that recorded only the organization could not say which
    /// of six committee members published an event that turned out to be a scam.
    /// </summary>
    public UserId OrganizerUserId { get; private set; } = default!;

    /// <summary>
    /// Who the event is billed as being by, when that differs from the organizer. A church's
    /// youth ministry running an event under the church's name, for instance.
    /// </summary>
    public string? HostName { get; private set; }

    /// <summary>
    /// The event-category catalogue key. A key rather than an identifier because the catalogue is
    /// configurable — see <see cref="Catalog.EventCategory"/>.
    /// </summary>
    public string CategoryKey { get; private set; } = string.Empty;

    // ── Series membership ───────────────────────────────────────────────────────────────────
    //
    // B12.1. Four nullable columns, and no second aggregate. An occurrence of a weekly meetup is
    // an event in every respect that matters — it has a guest list, a capacity, a check-in desk and
    // an analytics row — so it is one, and these four fields are the whole of what makes it also a
    // member of a pattern. Modelling occurrences separately would have meant a second registration
    // model and a second attendance model beside the ones B12 already keeps reconcilable.

    /// <summary>
    /// The series that generated it, when one did. Null for the great majority of events, which are
    /// one occasion and not a pattern.
    /// </summary>
    public EventSeriesId? SeriesId { get; private set; }

    /// <summary>
    /// Its 1-based ordinal in the series' generated dates.
    /// <para>
    /// <b>Never renumbered.</b> Cancelling the third of ten leaves the fourth as the fourth, which
    /// is what lets "this and future" be a comparison rather than a recount, and what gives the
    /// occurrence page a tie-break that is unique within its series.
    /// </para>
    /// </summary>
    public int? SeriesSequence { get; private set; }

    /// <summary>
    /// The instant the rule generated, when the occurrence has since been moved off it.
    /// <para>
    /// Null on an occurrence still sitting where the rule put it. Set once and never updated, so an
    /// occurrence moved twice still reports the slot it came from — which is the date the series
    /// holds an exception for.
    /// </para>
    /// </summary>
    public DateTimeOffset? OriginalStartsAt { get; private set; }

    /// <summary>
    /// Whether the occurrence has been edited on its own and is no longer the rule's to rewrite.
    /// <para>
    /// The organizer who moved one week's meeting to a different hall meant it, and a later
    /// series-wide edit that quietly moved it back would undo a deliberate act without saying so.
    /// </para>
    /// </summary>
    public bool IsSeriesException { get; private set; }

    // ── Content ─────────────────────────────────────────────────────────────────────────────

    public CommunityLifeTitle Title { get; private set; } = default!;

    public CommunityLifeSlug Slug { get; private set; } = default!;

    public CommunityLifeDescription Description { get; private set; } = default!;

    public EventSchedule Schedule { get; private set; } = default!;

    public CommunityLifeLocationContext Location { get; private set; } = default!;

    public EventVenue Venue { get; private set; } = default!;

    public EventAttendanceMode AttendanceMode { get; private set; }

    public EventCapacity Capacity { get; private set; } = default!;

    public EventAudience Audience { get; private set; } = default!;

    public ContactDetails Contact { get; private set; } = default!;

    // ── Owned collections ───────────────────────────────────────────────────────────────────
    //
    // Every field is named for the property in front of it. EF finds a read-only collection's
    // backing field by that naming convention and by nothing else; a mismatch compiles, saves, and
    // throws "Collection is read-only" the first time a row with one is read back.

    public IReadOnlyList<EventAgendaItem> AgendaItems =>
        _agendaItems.OrderBy(item => item.Order).ToList().AsReadOnly();

    public IReadOnlyList<EventSpeaker> Speakers =>
        _speakers.OrderBy(speaker => speaker.Order).ToList().AsReadOnly();

    public IReadOnlyList<EventMediaReference> MediaReferences => _mediaReferences.AsReadOnly();

    public IReadOnlyList<EventGuideReference> GuideReferences => _guideReferences.AsReadOnly();

    public IReadOnlyList<EventProviderReference> ProviderReferences =>
        _providerReferences.AsReadOnly();

    public IReadOnlyList<EventLinkedResource> LinkedResources =>
        _linkedResources.OrderBy(resource => resource.Order).ToList().AsReadOnly();

    public IReadOnlyList<EventModerationNote> ModerationNotes => _moderationNotes.AsReadOnly();

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public EventStatus Status { get; private set; }

    public EventVisibility Visibility { get; private set; }

    public CommunityLifeModerationState ModerationState { get; private set; }

    public EventEngagement Engagement { get; private set; } = default!;

    /// <summary>
    /// Whether the event takes responses at all.
    /// <para>
    /// False for a Sunday service or a public festival, where an RSVP means nothing and asking for
    /// one just puts a barrier between a newcomer and the door. When false, no RSVP may be created
    /// and the capacity counters stay at zero.
    /// </para>
    /// </summary>
    public bool RequiresRsvp { get; private set; }

    /// <summary>
    /// Whether people who are not going may see who is. Off by default: an attendee list is the
    /// most socially sensitive thing this module holds, and a member who said they were coming to
    /// an immigration advice session did not consent to being listed publicly for having done so.
    /// </summary>
    public bool IsAttendeeListPublic { get; private set; }

    public bool IsFeatured { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>The first publication, which never moves. What time-to-fill is measured from.</summary>
    public DateTimeOffset? FirstPublishedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Distinct members who have reported it. Drives the auto-flag threshold.</summary>
    public int ReportCount { get; private set; }

    /// <summary>Bumped on every content change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    public EventMediaReference? CoverImage =>
        _mediaReferences.FirstOrDefault(
            reference => reference.Role == CommunityLifeMediaRole.Cover)
        ?? _mediaReferences.Where(reference => reference.Role == CommunityLifeMediaRole.Gallery)
            .OrderBy(reference => reference.Order)
            .FirstOrDefault();

    /// <summary>Whether anybody outside its organizers can reach it.</summary>
    public bool IsDiscoverable =>
        !IsDeleted
        && EventStatusTransitions.IsDiscoverable(Status)
        && Visibility is EventVisibility.Public or EventVisibility.MembersOnly
        && ModerationState is CommunityLifeModerationState.Clear
            or CommunityLifeModerationState.Flagged;

    /// <summary>
    /// Whether a stranger could find it by searching right now — the events search's own rule for an
    /// anonymous viewer: live (published or in progress), <see cref="EventVisibility.Public"/>, and
    /// neither hidden nor suspended.
    /// <para>
    /// <b>Narrower than <see cref="IsDiscoverable"/> on purpose.</b> A members-only event is
    /// discoverable to its organization's members, and an unlisted one to whoever holds the link; a
    /// consumer that tells people it does not know about an event — an automation fired for anybody
    /// watching, a saved search — can honour neither audience, so for those it is the host's alone.
    /// 21 Oct 2026.
    /// </para>
    /// </summary>
    public bool IsListedPublicly =>
        IsDiscoverable && Visibility == EventVisibility.Public;

    /// <summary>
    /// Whether somebody may respond right now.
    /// <para>
    /// <b>This is the "Open for RSVP" the brief draws as a lifecycle state, and it is a predicate
    /// rather than a status on purpose.</b> Whether the door is open is a fact about the deadline,
    /// the capacity and the moderation state, and a status would mean a background job had to move
    /// every event twice a day or the door would never open.
    /// </para>
    /// </summary>
    public bool AcceptsRsvpAt(DateTimeOffset now) =>
        !IsDeleted
        && RequiresRsvp
        && EventStatusTransitions.AcceptsRsvp(Status)
        && ModerationState is CommunityLifeModerationState.Clear
            or CommunityLifeModerationState.Flagged
        && Schedule.AcceptsRegistrationAt(now);

    /// <summary>Whether attendance may be taken right now. Likewise derived, not stored.</summary>
    public bool AcceptsCheckInAt(DateTimeOffset now) =>
        !IsDeleted
        && EventStatusTransitions.AcceptsCheckIn(Status)
        && Schedule.AcceptsCheckInAt(now);

    /// <summary>Whether somebody responding now would be waitlisted rather than admitted.</summary>
    public bool IsFull => Capacity.IsFull;

    /// <summary>Whether this event is one dated instance of a repeating pattern.</summary>
    public bool IsOccurrence => SeriesId is not null;

    /// <summary>Whether the event has the minimum somebody needs to decide whether to come.</summary>
    public bool IsPublishable =>
        Description.Summary.Length > 0
        && CategoryKey.Length > 0
        && (AttendanceMode == EventAttendanceMode.Online || Venue.IsAddressable)
        && (AttendanceMode == EventAttendanceMode.InPerson || Venue.HasOnlineAccess)
        && ModerationState is not (CommunityLifeModerationState.Hidden
            or CommunityLifeModerationState.Suspended);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records the community the host posted this event into. Creation only; the caller has asked
    /// the community's own module first. Refused for an empty identifier or once already set.
    /// </summary>
    public Result PostInCommunity(Guid communityId)
    {
        if (communityId == Guid.Empty || CommunityId is not null)
        {
            return Result.Failure(CommunityLifeErrors.CommunityPostingForbidden);
        }

        CommunityId = communityId;
        return Result.Success();
    }

    /// <summary>
    /// Drafts an event.
    /// <para>
    /// Always into <see cref="EventStatus.Draft"/> and <see cref="EventVisibility.Private"/>,
    /// whatever the caller asked for. Publishing is a separate act with its own permission, its own
    /// readiness checks and its own spam pass; a create that could publish would skip all three.
    /// </para>
    /// </summary>
    public static Result<CommunityEvent> Draft(
        CommunityEventId id,
        OrganizationId? organizationId,
        UserId organizerUserId,
        CommunityLifeTitle title,
        CommunityLifeSlug slug,
        CommunityLifeDescription description,
        string? categoryKey,
        EventSchedule schedule,
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
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(venue);

        var key = Organizations.Organization.NormalizeKey(categoryKey);

        if (key is null)
        {
            return Result.Failure<CommunityEvent>(CommunityLifeErrors.EventCategoryInvalid);
        }

        var placed = EnsureVenueMatchesMode(venue, attendanceMode);

        if (placed.IsFailure)
        {
            return Result.Failure<CommunityEvent>(placed.Error);
        }

        var communityEvent = new CommunityEvent(
            id,
            organizationId,
            organizerUserId,
            title,
            slug,
            description,
            key,
            schedule,
            location,
            venue,
            attendanceMode,
            createdAt);

        communityEvent.RaiseDomainEvent(new CommunityEventCreatedDomainEvent
        {
            CommunityEventId = id,
            OrganizationId = organizationId,
            CreatedBy = organizerUserId,
            Title = title.Value,
            Slug = slug.Value,
            CategoryKey = key,
            CountryCode = location.CountryCode,
            NeighborhoodAreaId = location.NeighborhoodAreaId,
            StartsAt = schedule.StartsAt
        });

        return Result.Success(communityEvent);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Content
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Replaces the event's descriptive content.
    /// <para>
    /// Every parameter is optional and null means "leave it alone", so a client editing one field
    /// need not round-trip the other eight — and cannot blank a field by omitting it.
    /// </para>
    /// </summary>
    public Result<IReadOnlyList<string>> UpdateContent(
        int? baseRevision,
        UserId? changedBy,
        CommunityLifeTitle? title = null,
        CommunityLifeDescription? description = null,
        EventAudience? audience = null,
        ContactDetails? contact = null,
        string? hostName = null,
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

        if (requiresRsvp is { } takesRsvp && takesRsvp != RequiresRsvp)
        {
            // Turning responses off on an event people have already responded to would strand
            // their places. The counters stay; the door simply closes.
            RequiresRsvp = takesRsvp;
            changed.Add(nameof(RequiresRsvp));
        }

        if (isAttendeeListPublic is { } listPublic && listPublic != IsAttendeeListPublic)
        {
            IsAttendeeListPublic = listPublic;
            changed.Add(nameof(IsAttendeeListPublic));
        }

        if (changed.Count == 0)
        {
            return Result.Success<IReadOnlyList<string>>([]);
        }

        Touch();
        RaiseUpdated(changed, changedBy, isMaterialChange: false);

        return Result.Success<IReadOnlyList<string>>(changed);
    }

    /// <summary>
    /// Moves the event in time.
    /// <para>
    /// <b>Raises a material change</b>, which is what the notification path keys on. Everybody who
    /// said they were coming has to be told an event moved, and telling them about a corrected typo
    /// as well is how a platform trains people to ignore its messages.
    /// </para>
    /// </summary>
    public Result Reschedule(
        EventSchedule schedule, int? baseRevision, UserId? changedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (schedule == Schedule)
        {
            return Result.Success();
        }

        Schedule = schedule;
        ModifiedAt = now;

        Touch();
        RaiseUpdated([nameof(Schedule)], changedBy, isMaterialChange: true);

        return Result.Success();
    }

    /// <summary>
    /// Moves the event in space.
    /// <para>
    /// Material, for the same reason a reschedule is: somebody planning to travel to a hall needs
    /// to know it is now a different hall.
    /// </para>
    /// </summary>
    public Result Relocate(
        CommunityLifeLocationContext location,
        EventVenue venue,
        EventAttendanceMode attendanceMode,
        int? baseRevision,
        UserId? changedBy)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(venue);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var placed = EnsureVenueMatchesMode(venue, attendanceMode);

        if (placed.IsFailure)
        {
            return placed;
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

        if (changed.Count == 0)
        {
            return Result.Success();
        }

        Touch();
        RaiseUpdated(changed, changedBy, isMaterialChange: true);

        return Result.Success();
    }

    /// <summary>Restates the ceiling. Refused below what is already committed.</summary>
    public Result SetCapacity(int? maximum, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var updated = Capacity.WithMaximum(maximum);

        if (updated.IsFailure)
        {
            return Result.Failure(updated.Error);
        }

        Capacity = updated.Value;

        Touch();
        RaiseUpdated([nameof(Capacity)], null, isMaterialChange: false);

        return Result.Success();
    }

    /// <summary>Moves the event to a different catalogue category.</summary>
    public Result ChangeCategory(string? categoryKey, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var key = Organizations.Organization.NormalizeKey(categoryKey);

        if (key is null)
        {
            return Result.Failure(CommunityLifeErrors.EventCategoryInvalid);
        }

        if (string.Equals(key, CategoryKey, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        CategoryKey = key;

        Touch();
        RaiseUpdated([nameof(CategoryKey)], null, isMaterialChange: false);

        return Result.Success();
    }

    /// <summary>Replaces the programme wholesale.</summary>
    public Result SetAgenda(IReadOnlyList<EventAgendaItem> items, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(items);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var kept = items.Where(item => item.Title.Length > 0).ToList();

        if (kept.Count > CommunityLifeLimits.MaxAgendaItems)
        {
            return Result.Failure(CommunityLifeErrors.AgendaLimit);
        }

        _agendaItems.Clear();
        _agendaItems.AddRange(kept);

        Touch();
        RaiseUpdated([nameof(AgendaItems)], null, isMaterialChange: false);

        return Result.Success();
    }

    /// <summary>Replaces the speaker list wholesale.</summary>
    public Result SetSpeakers(IReadOnlyList<EventSpeaker> speakers, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(speakers);

        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        var kept = speakers.Where(speaker => speaker.Name.Length > 0).ToList();

        if (kept.Count > CommunityLifeLimits.MaxSpeakers)
        {
            return Result.Failure(CommunityLifeErrors.SpeakerLimit);
        }

        _speakers.Clear();
        _speakers.AddRange(kept);

        Touch();
        RaiseUpdated([nameof(Speakers)], null, isMaterialChange: false);

        return Result.Success();
    }

    /// <summary>Replaces the linked resources — job postings, vendor booths, related sessions.</summary>
    public Result SetLinkedResources(IReadOnlyList<EventLinkedResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (resources.Count > CommunityLifeLimits.MaxLinkedResources)
        {
            return Result.Failure(CommunityLifeErrors.LinkedResourceLimit);
        }

        _linkedResources.Clear();
        _linkedResources.AddRange(
            resources
                .GroupBy(resource => (resource.ResourceType, resource.ResourceId, resource.Url))
                .Select(group => group.First()));

        Touch();

        return Result.Success();
    }

    /// <summary>Replaces the provider references — the training company, the venue, the caterer.</summary>
    public Result SetProviderReferences(IReadOnlyList<EventProviderReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (references.Count > CommunityLifeLimits.MaxProviderReferences)
        {
            return Result.Failure(CommunityLifeErrors.ProviderReferenceInvalid);
        }

        _providerReferences.Clear();
        _providerReferences.AddRange(
            references.GroupBy(reference => reference.ProviderId).Select(group => group.First()));

        Touch();

        return Result.Success();
    }

    /// <summary>Replaces the contextual guide references.</summary>
    public Result SetGuideReferences(IReadOnlyList<EventGuideReference> references)
    {
        ArgumentNullException.ThrowIfNull(references);

        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (references.Count > CommunityLifeLimits.MaxGuideReferences)
        {
            return Result.Failure(CommunityLifeErrors.GuideReferenceInvalid);
        }

        _guideReferences.Clear();
        _guideReferences.AddRange(
            references.GroupBy(reference => reference.GuideId).Select(group => group.First()));

        Touch();

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Media
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Attaches a Media asset by reference.
    /// <para>
    /// Idempotent on (media, role). Attaching a second cover replaces the first, because there is
    /// one banner and an event with two would render whichever the database returned first.
    /// </para>
    /// </summary>
    public Result AttachMedia(
        Guid mediaId, CommunityLifeMediaRole role, string? caption, DateTimeOffset now)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (mediaId == Guid.Empty)
        {
            return Result.Failure(CommunityLifeErrors.MediaInvalid);
        }

        if (_mediaReferences.Count >= CommunityLifeLimits.MaxMediaReferences)
        {
            return Result.Failure(CommunityLifeErrors.MediaLimit);
        }

        if (_mediaReferences.Any(
                reference => reference.MediaId == mediaId && reference.Role == role))
        {
            return Result.Success();
        }

        if (role is CommunityLifeMediaRole.Cover or CommunityLifeMediaRole.Logo)
        {
            _mediaReferences.RemoveAll(reference => reference.Role == role);
        }

        var order = _mediaReferences.Count(reference => reference.Role == role);

        _mediaReferences.Add(EventMediaReference.Create(mediaId, role, order, caption));

        Touch();
        ModifiedAt = now;

        RaiseDomainEvent(new CommunityEventMediaAttachedDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            MediaId = mediaId,
            Role = role
        });

        return Result.Success();
    }

    /// <summary>Removes a media reference. Never deletes the asset — that is Media's decision.</summary>
    public Result DetachMedia(Guid mediaId)
    {
        var guard = EnsureEditable(baseRevision: null);

        if (guard.IsFailure)
        {
            return guard;
        }

        var reference = _mediaReferences.FirstOrDefault(item => item.MediaId == mediaId);

        if (reference is null)
        {
            return Result.Success();
        }

        _mediaReferences.Remove(reference);
        Touch();

        RaiseDomainEvent(new CommunityEventMediaDetachedDomainEvent
        {
            CommunityEventId = Id,
            MediaId = mediaId,
            Role = reference.Role
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Announces the event.</summary>
    public Result Publish(DateTimeOffset now, UserId? publishedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.EventNotFound);
        }

        if (Status == EventStatus.Published)
        {
            return Result.Failure(CommunityLifeErrors.EventAlreadyPublished);
        }

        if (!IsPublishable)
        {
            return Result.Failure(CommunityLifeErrors.EventNotPublishable);
        }

        if (Schedule.HasEnded(now))
        {
            return Result.Failure(CommunityLifeErrors.ScheduleInPast);
        }

        var moved = MoveTo(EventStatus.Published, now, publishedBy, reason: null);

        if (moved.IsFailure)
        {
            return moved;
        }

        var isFirstPublication = FirstPublishedAt is null;

        PublishedAt = now;
        FirstPublishedAt ??= now;
        Visibility = Visibility == EventVisibility.Private
            ? EventVisibility.Public
            : Visibility;

        RaiseDomainEvent(new CommunityEventPublishedDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            Title = Title.Value,
            Slug = Slug.Value,
            CategoryKey = CategoryKey,
            CountryCode = Location.CountryCode,
            NeighborhoodAreaId = Location.NeighborhoodAreaId,
            StartsAt = Schedule.StartsAt,
            IsFirstPublication = isFirstPublication,
            PublishedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// The general lifecycle move. Every transition goes through here so the machine is consulted
    /// once and the status event is raised once.
    /// </summary>
    public Result MoveTo(
        EventStatus target,
        DateTimeOffset now,
        UserId? changedBy = null,
        string? reason = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.EventNotFound);
        }

        if (Status == target)
        {
            return Result.Success();
        }

        if (!EventStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(CommunityLifeErrors.EventTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        ModifiedAt = now;

        if (target is EventStatus.Draft or EventStatus.Archived)
        {
            Visibility = EventVisibility.Private;
        }

        RaiseDomainEvent(new CommunityEventStatusChangedDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Calls the event off.
    /// <para>
    /// <b>Raises its own event rather than relying on the status change</b>, because it is the one
    /// transition with a consequence for other people: every place has to be released and everybody
    /// who was coming has to be told. The affected count travels with it so a consumer can size the
    /// notification without loading the guest list.
    /// </para>
    /// </summary>
    public Result Cancel(DateTimeOffset now, UserId? cancelledBy, string? reason)
    {
        // Read before the move: a cancelled event is not discoverable, and whether strangers could
        // see it a moment ago is what decides who may be told it is off. A cancelled draft was never
        // anybody's but its organiser's.
        var wasListedPublicly = IsListedPublicly;

        var moved = MoveTo(EventStatus.Cancelled, now, cancelledBy, reason);

        if (moved.IsFailure)
        {
            return moved;
        }

        CancelledAt = now;
        CancellationReason = Clip(reason, CommunityLifeLimits.MaxNoteLength);

        RaiseDomainEvent(new CommunityEventCancelledDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            Title = Title.Value,
            StartsAt = Schedule.StartsAt,
            AffectedAttendeeCount = Capacity.GoingCount,
            CancelledBy = cancelledBy,
            Reason = CancellationReason,
            CancelledAt = now,
            WasListedPublicly = wasListedPublicly
        });

        return Result.Success();
    }

    /// <summary>
    /// Marks the event finished and freezes its attendance figures.
    /// <para>
    /// Called by the sweep from the event's own end time rather than by the organizer, which is the
    /// only honest source: somebody who has just run a festival is not going to press a button.
    /// </para>
    /// </summary>
    public Result Complete(DateTimeOffset now, int noShowCount = 0)
    {
        var moved = MoveTo(EventStatus.Completed, now);

        if (moved.IsFailure)
        {
            return moved;
        }

        CompletedAt = now;

        RaiseDomainEvent(new CommunityEventCompletedDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            CategoryKey = CategoryKey,
            GoingCount = Capacity.GoingCount,
            CheckedInCount = Capacity.CheckedInCount,
            NoShowCount = Math.Max(0, noShowCount),
            CompletedAt = now
        });

        return Result.Success();
    }

    /// <summary>Files the event away. Reversible only through a restore into draft.</summary>
    public Result Archive(DateTimeOffset now, UserId? changedBy, string? reason) =>
        MoveTo(EventStatus.Archived, now, changedBy, reason);

    /// <summary>
    /// Brings an archived event back, as a draft.
    /// <para>
    /// Never straight to published: an archived event's date has almost certainly passed, and
    /// republishing it unexamined would put a stale date at the top of a discovery feed and reopen
    /// responses on something nobody can attend.
    /// </para>
    /// </summary>
    public Result RestoreFromArchive(DateTimeOffset now, UserId? changedBy)
    {
        if (Status != EventStatus.Archived)
        {
            return Result.Failure(CommunityLifeErrors.EventNotArchived);
        }

        var moved = MoveTo(EventStatus.Draft, now, changedBy, reason: null);

        if (moved.IsFailure)
        {
            return moved;
        }

        RaiseDomainEvent(new CommunityEventRestoredDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            WasDeleted = false
        });

        return Result.Success();
    }

    /// <summary>Changes who can reach the event, independent of its lifecycle state.</summary>
    public Result SetVisibility(EventVisibility visibility, int? baseRevision)
    {
        var guard = EnsureEditable(baseRevision);

        if (guard.IsFailure)
        {
            return guard;
        }

        // A members-only event with no organization behind it has no membership to check against,
        // and would be unreachable by everybody including its own organizer's community.
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
        RaiseUpdated([nameof(Visibility)], null, isMaterialChange: false);

        return Result.Success();
    }

    /// <summary>Promotes or demotes the event. Staff only, gated at the endpoint.</summary>
    public void SetFeatured(bool isFeatured, DateTimeOffset now)
    {
        if (IsFeatured == isFeatured)
        {
            return;
        }

        IsFeatured = isFeatured;
        ModifiedAt = now;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Series membership
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Binds a freshly drafted event to the series that generated it.
    /// <para>
    /// Called once, by the expansion service, before the row is written. Refused afterwards: an
    /// event that already exists on its own has its own guest list and its own link, and folding it
    /// into a pattern retrospectively would make a rule the owner of something it did not create.
    /// </para>
    /// </summary>
    public Result AttachToSeries(EventSeriesId seriesId, int sequence)
    {
        ArgumentNullException.ThrowIfNull(seriesId);

        if (SeriesId is not null)
        {
            return Result.Failure(CommunityLifeErrors.EventSeriesTransitionInvalid);
        }

        SeriesId = seriesId;
        SeriesSequence = Math.Max(1, sequence);

        return Result.Success();
    }

    /// <summary>
    /// Moves one occurrence off the date its rule generated.
    /// <para>
    /// <b>Its own act rather than an ordinary reschedule, and the difference is what gets said to
    /// the people coming.</b> Moving a one-off event tells its attendees the date changed; moving
    /// one week of a weekly meeting has to tell them <em>this week</em> changed and the pattern did
    /// not. The generated slot is recorded on the first move and never afterwards, because that slot
    /// — not the previous start — is the date the series holds an exception for.
    /// </para>
    /// </summary>
    public Result RescheduleOccurrence(
        EventSchedule schedule, int? baseRevision, UserId? changedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        if (SeriesId is not { } seriesId)
        {
            return Result.Failure(CommunityLifeErrors.OccurrenceNotFound);
        }

        if (Status is EventStatus.Cancelled or EventStatus.Completed or EventStatus.Archived)
        {
            return Result.Failure(CommunityLifeErrors.OccurrenceNotMovable);
        }

        var previousStart = Schedule.StartsAt;
        var generatedStart = OriginalStartsAt ?? previousStart;

        var moved = Reschedule(schedule, baseRevision, changedBy, now);

        if (moved.IsFailure)
        {
            return moved;
        }

        OriginalStartsAt ??= generatedStart;
        IsSeriesException = true;

        RaiseDomainEvent(new EventOccurrenceRescheduledDomainEvent
        {
            CommunityEventId = Id,
            EventSeriesId = seriesId,
            OrganizationId = OrganizationId,
            OriginalStartsAt = generatedStart,
            PreviousStartsAt = previousStart,
            CurrentStartsAt = schedule.StartsAt,
            AffectedAttendeeCount = Capacity.GoingCount,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Marks the occurrence as one the rule no longer rewrites.
    /// <para>
    /// Set by an individual cancellation as well as by a move, so a later series-wide edit steps
    /// over both. Deliberately not a revision bump: it is bookkeeping about who owns the row, not a
    /// change to what the row says, and an organizer's in-flight save must not fail because of it.
    /// </para>
    /// </summary>
    public void MarkSeriesException()
    {
        if (SeriesId is not null)
        {
            IsSeriesException = true;
        }
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Attendance
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Applies the outcome of an RSVP to the attendance counters.
    /// <para>
    /// <b>The single settlement path, called only from the RSVP status handler.</b> Every place
    /// taken, released and used in the module arrives here, which is what makes the counts
    /// reconcilable: there is exactly one place to read to know how a place can move.
    /// </para>
    /// <para>
    /// Deliberately does not bump the revision. Somebody responding while the organizer edits the
    /// event must not make the organizer's save fail — which is precisely what would happen on the
    /// morning of any popular event.
    /// </para>
    /// </summary>
    public void SettleAttendance(
        DateTimeOffset now,
        int going = 0,
        int interested = 0,
        int waitlisted = 0,
        int checkedIn = 0)
    {
        Capacity = Capacity.Apply(going, interested, waitlisted, checkedIn);
        ModifiedAt = now;
    }

    /// <summary>Restates the counters from a recount. The repair path, not the normal one.</summary>
    public void ReconcileAttendance(
        int going, int interested, int waitlisted, int checkedIn, DateTimeOffset now)
    {
        Capacity = Capacity.Reconcile(going, interested, waitlisted, checkedIn);
        ModifiedAt = now;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Engagement
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a view.
    /// <para>
    /// Deliberately does not bump the revision, for the same reason attendance does not. A member
    /// opening an event while its organizer is editing it must not make the organizer's save fail.
    /// </para>
    /// </summary>
    public void RecordView(bool isUnique) => Engagement = Engagement.RecordView(isUnique);

    public void RecordSave() => Engagement = Engagement.RecordSave();

    public void RecordUnsave() => Engagement = Engagement.RecordUnsave();

    public void RecordShare() => Engagement = Engagement.RecordShare();

    public void RecordRsvp() => Engagement = Engagement.RecordRsvp();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Moderation & safety
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Records a report and flags the event when the threshold is met.</summary>
    public bool RecordReport(int reportsBeforeAutoFlag, DateTimeOffset now, UserId? reportedBy)
    {
        ReportCount++;
        ModifiedAt = now;

        if (ModerationState != CommunityLifeModerationState.Clear
            || ReportCount < Math.Max(1, reportsBeforeAutoFlag))
        {
            return false;
        }

        ChangeModeration(
            CommunityLifeModerationState.Flagged,
            reportedBy,
            "Automatically flagged by reports.",
            now);

        return true;
    }

    /// <summary>Removes the event from discovery. Its organizer can still see and fix it.</summary>
    public void Hide(string? reason, UserId? moderatorId, DateTimeOffset now) =>
        ChangeModeration(CommunityLifeModerationState.Hidden, moderatorId, reason, now);

    /// <summary>Removes the event and bars republication pending a decision about its organizer.</summary>
    public void Suspend(string? reason, UserId? moderatorId, DateTimeOffset now) =>
        ChangeModeration(CommunityLifeModerationState.Suspended, moderatorId, reason, now);

    /// <summary>Clears a flag, a hide or a suspension.</summary>
    public void Clear(UserId? moderatorId, DateTimeOffset now) =>
        ChangeModeration(CommunityLifeModerationState.Clear, moderatorId, null, now);

    /// <summary>
    /// Adds a note to the editorial trail.
    /// <para>
    /// Deliberately not a content edit: it raises no update event and does not bump the revision,
    /// so annotating an event cannot make the organizer's in-flight save fail.
    /// </para>
    /// </summary>
    public void AddModerationNote(Guid noteId, UserId authorId, string? note, DateTimeOffset now)
    {
        if (_moderationNotes.Count >= CommunityLifeLimits.MaxModerationNotes)
        {
            return;
        }

        _moderationNotes.Add(EventModerationNote.Create(noteId, authorId, note, now));
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new CommunityEventDeletedDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            DeletedAt = timestamp
        });
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;

        RaiseDomainEvent(new CommunityEventRestoredDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            WasDeleted = true
        });
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

    /// <summary>Rejects a save made against a revision somebody else has already superseded.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(CommunityLifeErrors.RevisionConflict)
            : Result.Success();

    /// <summary>
    /// An in-person event needs somewhere to be, and an online-only one must not claim a hall.
    /// Both refusals exist because the alternative is a member turning up somewhere.
    /// </summary>
    private static Result EnsureVenueMatchesMode(EventVenue venue, EventAttendanceMode mode)
    {
        if (mode is EventAttendanceMode.InPerson or EventAttendanceMode.Hybrid
            && !venue.IsAddressable)
        {
            return Result.Failure(CommunityLifeErrors.VenueRequired);
        }

        if (mode == EventAttendanceMode.Online && venue.IsAddressable)
        {
            return Result.Failure(CommunityLifeErrors.VenueNotPermitted);
        }

        return Result.Success();
    }

    private void ChangeModeration(
        CommunityLifeModerationState state,
        UserId? moderatorId,
        string? reason,
        DateTimeOffset now)
    {
        if (ModerationState == state)
        {
            return;
        }

        var previous = ModerationState;

        ModerationState = state;
        ModifiedAt = now;

        RaiseDomainEvent(new CommunityEventModerationChangedDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            PreviousState = previous,
            CurrentState = state,
            ChangedBy = moderatorId,
            Reason = reason
        });
    }

    private Result EnsureEditable(int? baseRevision)
    {
        if (IsDeleted)
        {
            return Result.Failure(CommunityLifeErrors.EventNotFound);
        }

        if (Status == EventStatus.Archived)
        {
            return Result.Failure(CommunityLifeErrors.EventArchivedImmutable);
        }

        if (Status == EventStatus.Cancelled)
        {
            return Result.Failure(CommunityLifeErrors.EventCancelled);
        }

        if (Status == EventStatus.Completed)
        {
            return Result.Failure(CommunityLifeErrors.EventCompleted);
        }

        return EnsureRevision(baseRevision);
    }

    private void RaiseUpdated(
        IReadOnlyList<string> changedFields, UserId? changedBy, bool isMaterialChange) =>
        RaiseDomainEvent(new CommunityEventUpdatedDomainEvent
        {
            CommunityEventId = Id,
            OrganizationId = OrganizationId,
            ChangedBy = changedBy,
            ChangedFields = changedFields,
            IsMaterialChange = isMaterialChange
        });

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
