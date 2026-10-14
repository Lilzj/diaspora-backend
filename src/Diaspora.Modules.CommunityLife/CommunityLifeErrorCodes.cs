namespace Diaspora.Modules.CommunityLife;

/// <summary>
/// The stable wire-level error codes the Community Life module emits.
/// <para>
/// These strings are frozen contract: clients switch on them, support searches logs by them, and
/// translations key off them. Change the description on an <c>Error</c> freely; never change a
/// code. New failures get new codes.
/// </para>
/// </summary>
public static class CommunityLifeErrorCodes
{
    private const string Prefix = "community-life";

    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public const string OrganizationNotFound = Prefix + ".organization.not-found";
    public const string EventNotFound = Prefix + ".event.not-found";
    public const string MembershipNotFound = Prefix + ".membership.not-found";
    public const string RsvpNotFound = Prefix + ".rsvp.not-found";
    public const string ReportNotFound = Prefix + ".report.not-found";
    public const string OrganizationTypeNotFound = Prefix + ".organization-type.not-found";
    public const string EventCategoryNotFound = Prefix + ".event-category.not-found";
    public const string NeighborhoodNotFound = Prefix + ".neighborhood.not-found";
    public const string AreaPointInvalid = Prefix + ".area.point-invalid";
    public const string CountryInvalid = Prefix + ".country.invalid";
    public const string CoordinatesInvalid = Prefix + ".coordinates.invalid";

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public const string NotAuthenticated = Prefix + ".not-authenticated";
    public const string NotOrganizationMember = Prefix + ".organization.not-member";
    public const string NotOrganizationAdministrator = Prefix + ".organization.not-administrator";
    public const string NotOrganizationOwner = Prefix + ".organization.not-owner";
    public const string NotEventOrganizer = Prefix + ".event.not-organizer";
    public const string NotRsvpOwner = Prefix + ".rsvp.not-owner";
    public const string NotModerator = Prefix + ".not-moderator";
    public const string NotProviderAgent = Prefix + ".provider.not-agent";
    public const string InsufficientMembershipRole = Prefix + ".membership.role-insufficient";

    // ── Organization lifecycle ──────────────────────────────────────────────────────────────

    public const string OrganizationTransitionInvalid = Prefix + ".organization.transition-invalid";
    public const string OrganizationAlreadyPublished = Prefix + ".organization.already-published";
    public const string OrganizationNotPublishable = Prefix + ".organization.not-publishable";
    public const string OrganizationNotArchived = Prefix + ".organization.not-archived";
    public const string OrganizationArchivedImmutable = Prefix + ".organization.archived-immutable";
    public const string OrganizationSuspended = Prefix + ".organization.suspended";
    public const string DuplicateOrganization = Prefix + ".organization.duplicate";
    public const string TooManyOrganizations = Prefix + ".organization.too-many";
    public const string OwnerCannotLeave = Prefix + ".organization.owner-cannot-leave";

    // ── Event lifecycle ─────────────────────────────────────────────────────────────────────

    public const string EventTransitionInvalid = Prefix + ".event.transition-invalid";
    public const string EventAlreadyPublished = Prefix + ".event.already-published";
    public const string EventNotPublishable = Prefix + ".event.not-publishable";
    public const string EventNotArchived = Prefix + ".event.not-archived";
    public const string EventArchivedImmutable = Prefix + ".event.archived-immutable";
    public const string EventCancelled = Prefix + ".event.cancelled";
    public const string EventCompleted = Prefix + ".event.completed";
    public const string DuplicateEvent = Prefix + ".event.duplicate";
    public const string TooManyEvents = Prefix + ".event.too-many";

    // ── Event content ───────────────────────────────────────────────────────────────────────

    public const string TitleInvalid = Prefix + ".title.invalid";
    public const string DescriptionInvalid = Prefix + ".description.invalid";
    public const string SlugInvalid = Prefix + ".slug.invalid";
    public const string ScheduleInvalid = Prefix + ".schedule.invalid";
    public const string ScheduleInPast = Prefix + ".schedule.in-past";
    public const string ScheduleTooFarAhead = Prefix + ".schedule.too-far-ahead";
    public const string ScheduleTooLong = Prefix + ".schedule.too-long";
    public const string TimeZoneInvalid = Prefix + ".timezone.invalid";
    public const string VenueRequired = Prefix + ".venue.required";
    public const string VenueNotPermitted = Prefix + ".venue.not-permitted";
    public const string CapacityInvalid = Prefix + ".capacity.invalid";
    public const string CapacityBelowCommitted = Prefix + ".capacity.below-committed";
    public const string RegistrationDeadlineInvalid = Prefix + ".registration-deadline.invalid";
    public const string AgendaLimit = Prefix + ".agenda.limit";
    public const string SpeakerLimit = Prefix + ".speaker.limit";
    public const string LinkedResourceLimit = Prefix + ".linked-resource.limit";
    public const string LinkedResourceInvalid = Prefix + ".linked-resource.invalid";
    public const string EventCategoryInvalid = Prefix + ".event-category.invalid";

    // ── Organization content ────────────────────────────────────────────────────────────────

    public const string OrganizationNameInvalid = Prefix + ".organization.name.invalid";
    public const string OrganizationTypeInvalid = Prefix + ".organization-type.invalid";
    public const string ContactInvalid = Prefix + ".contact.invalid";
    public const string SocialLinkInvalid = Prefix + ".social-link.invalid";
    public const string OperatingHoursInvalid = Prefix + ".operating-hours.invalid";
    public const string OperatingHoursLimit = Prefix + ".operating-hours.limit";
    public const string LeaderLimit = Prefix + ".leader.limit";
    public const string LanguageLimit = Prefix + ".language.limit";
    public const string ServiceLimit = Prefix + ".service.limit";

    // ── Membership ──────────────────────────────────────────────────────────────────────────

    public const string MembershipTransitionInvalid = Prefix + ".membership.transition-invalid";
    public const string AlreadyMember = Prefix + ".membership.already-member";
    public const string MembershipClosed = Prefix + ".membership.closed";
    public const string MembershipInviteOnly = Prefix + ".membership.invite-only";
    public const string MembershipRemoved = Prefix + ".membership.removed";
    public const string MembershipNotPending = Prefix + ".membership.not-pending";
    public const string MembershipRequirementsNotMet = Prefix + ".membership.requirements-not-met";
    public const string MembershipPolicyInvalid = Prefix + ".membership.policy-invalid";
    public const string TooManyMembershipRequests = Prefix + ".membership.too-many-requests";
    public const string EventHasAttendees = Prefix + ".event.has-attendees";
    public const string TooManyRsvps = Prefix + ".rsvp.too-many";
    public const string AttendeeListPrivate = Prefix + ".event.attendee-list-private";
    public const string ReportSubjectInvalid = Prefix + ".report.subject-invalid";
    public const string ReportReasonInvalid = Prefix + ".report.reason-invalid";
    public const string ModerationActionInvalid = Prefix + ".moderation.action-invalid";
    public const string LastOwner = Prefix + ".membership.last-owner";
    public const string RoleNotAssignable = Prefix + ".membership.role-not-assignable";

    // ── RSVP & check-in ─────────────────────────────────────────────────────────────────────

    public const string RsvpTransitionInvalid = Prefix + ".rsvp.transition-invalid";
    public const string RsvpNotRequired = Prefix + ".rsvp.not-required";
    public const string RsvpClosed = Prefix + ".rsvp.closed";
    public const string RsvpDeadlinePassed = Prefix + ".rsvp.deadline-passed";
    public const string EventFull = Prefix + ".event.full";
    public const string AlreadyRsvped = Prefix + ".rsvp.already-exists";
    public const string CheckInNotOpen = Prefix + ".checkin.not-open";
    public const string CheckInMethodUnavailable = Prefix + ".checkin.method-unavailable";
    public const string AlreadyCheckedIn = Prefix + ".checkin.already";
    public const string NotAttending = Prefix + ".checkin.not-attending";
    public const string EventNotMemberVisible = Prefix + ".event.members-only";

    // ── Media ───────────────────────────────────────────────────────────────────────────────

    public const string MediaInvalid = Prefix + ".media.invalid";
    public const string MediaLimit = Prefix + ".media.limit";
    public const string GuideReferenceInvalid = Prefix + ".guide-reference.invalid";
    public const string ProviderReferenceInvalid = Prefix + ".provider-reference.invalid";

    // ── Moderation & trust ──────────────────────────────────────────────────────────────────

    public const string AlreadyReported = Prefix + ".report.already";
    public const string CannotReportOwn = Prefix + ".report.own";
    public const string ReportAlreadyResolved = Prefix + ".report.already-resolved";
    public const string SpamHold = Prefix + ".spam.hold";
    public const string VerificationAlreadyPending = Prefix + ".verification.already-pending";
    public const string VerificationUnavailable = Prefix + ".verification.unavailable";
    public const string VerificationRefused = Prefix + ".verification.refused";

    // ── Recurring series & occurrences ──────────────────────────────────────────────────────

    public const string EventSeriesNotFound = Prefix + ".series.not-found";
    public const string EventSeriesTransitionInvalid = Prefix + ".series.transition-invalid";
    public const string EventSeriesTerminal = Prefix + ".series.terminal";
    public const string SeriesExceptionLimit = Prefix + ".series.exception-limit";
    public const string SeriesOccurrenceLimit = Prefix + ".series.occurrence-limit";
    public const string SeriesScopeInvalid = Prefix + ".series.scope-invalid";
    public const string OccurrenceNotFound = Prefix + ".occurrence.not-found";
    public const string OccurrenceNotMovable = Prefix + ".occurrence.not-movable";
    public const string RecurrenceIntervalInvalid = Prefix + ".recurrence.interval-invalid";
    public const string RecurrenceWeekdayInvalid = Prefix + ".recurrence.weekday-invalid";
    public const string RecurrenceMonthDayInvalid = Prefix + ".recurrence.month-day-invalid";
    public const string RecurrenceEndInvalid = Prefix + ".recurrence.end-invalid";
    public const string RecurrenceCountInvalid = Prefix + ".recurrence.count-invalid";
    public const string RecurrenceFrequencyInvalid = Prefix + ".recurrence.frequency-invalid";
    public const string RecurrenceProducesNothing = Prefix + ".recurrence.produces-nothing";

    // ── Concurrency & paging ────────────────────────────────────────────────────────────────

    public const string RevisionConflict = Prefix + ".revision.conflict";
    public const string CursorInvalid = Prefix + ".cursor.invalid";
    public const string CatalogueEntryExists = Prefix + ".catalogue.exists";
}
