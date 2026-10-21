using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.Errors;

/// <summary>
/// Every failure this module can return, as constructed <see cref="Error"/> values.
/// <para>
/// One instance per failure, built once, so a handler returns
/// <c>CommunityLifeErrors.EventNotFound</c> rather than assembling a code and a message at the
/// failure site. That keeps the message consistent between the places the same failure arises, and
/// makes the set enumerable — a unit test asserts every constant here has a matching entry in
/// <see cref="CommunityLifeErrorCodes"/> and vice versa.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each is what decides the HTTP status, through
/// <c>PlatformResults</c>. No place in the module names a number. Two choices are worth stating:
/// <see cref="RevisionConflict"/> is a conflict so an editor can offer a merge rather than an error
/// page, and everything a caller must not be able to probe for — a hidden event, a private
/// organization, a draft — is a not-found rather than a forbidden, because a 403 on a hidden event
/// confirms it exists.
/// </para>
/// <para>
/// <b><see cref="EventNotMemberVisible"/> is the deliberate exception to that rule.</b> It is a
/// forbidden rather than a not-found, because a members-only event held by an organization the
/// caller can plainly see is not a secret — the members-only setting is the point, and a member
/// told the event does not exist would never learn that joining is how they get in.
/// </para>
/// </summary>
public static class CommunityLifeErrors
{
    // ── Existence & visibility ──────────────────────────────────────────────────────────────

    public static readonly Error OrganizationNotFound = Error.NotFound(
        CommunityLifeErrorCodes.OrganizationNotFound,
        "That organization does not exist, or you cannot see it.");

    public static readonly Error EventNotFound = Error.NotFound(
        CommunityLifeErrorCodes.EventNotFound,
        "That event does not exist, or you cannot see it.");

    public static readonly Error MembershipNotFound = Error.NotFound(
        CommunityLifeErrorCodes.MembershipNotFound,
        "That membership does not exist.");

    public static readonly Error RsvpNotFound = Error.NotFound(
        CommunityLifeErrorCodes.RsvpNotFound,
        "You have not responded to that event.");

    public static readonly Error ReportNotFound = Error.NotFound(
        CommunityLifeErrorCodes.ReportNotFound,
        "That report does not exist.");

    public static readonly Error OrganizationTypeNotFound = Error.NotFound(
        CommunityLifeErrorCodes.OrganizationTypeNotFound,
        "We do not have that kind of organization.");

    public static readonly Error EventCategoryNotFound = Error.NotFound(
        CommunityLifeErrorCodes.EventCategoryNotFound,
        "We do not have that kind of event.");

    public static readonly Error NeighborhoodNotFound = Error.NotFound(
        CommunityLifeErrorCodes.NeighborhoodNotFound,
        "We do not know that neighbourhood.");

    public static readonly Error AreaPointInvalid = Error.Validation(
        CommunityLifeErrorCodes.AreaPointInvalid,
        "The area needs a latitude between -90 and 90, a longitude between -180 and 180, and a radius "
        + "above 0 and at most 10 km.");

    public static readonly Error CountryInvalid = Error.Validation(
        CommunityLifeErrorCodes.CountryInvalid,
        "We do not operate in that country yet.");

    public static readonly Error CoordinatesInvalid = Error.Validation(
        CommunityLifeErrorCodes.CoordinatesInvalid,
        "Those coordinates are not on the Earth. Send both a latitude and a longitude, or neither.");

    // ── Authorization & ownership ───────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        CommunityLifeErrorCodes.NotAuthenticated,
        "Sign in to do that.");

    public static readonly Error NotOrganizationMember = Error.Forbidden(
        CommunityLifeErrorCodes.NotOrganizationMember,
        "You are not a member of that organization.");

    public static readonly Error NotOrganizationAdministrator = Error.Forbidden(
        CommunityLifeErrorCodes.NotOrganizationAdministrator,
        "Only an administrator of that organization can do that.");

    public static readonly Error NotOrganizationOwner = Error.Forbidden(
        CommunityLifeErrorCodes.NotOrganizationOwner,
        "Only the owner of that organization can do that.");

    public static readonly Error NotEventOrganizer = Error.NotFound(
        CommunityLifeErrorCodes.NotEventOrganizer,
        "That event does not exist, or you cannot manage it.");

    public static readonly Error NotRsvpOwner = Error.Forbidden(
        CommunityLifeErrorCodes.NotRsvpOwner,
        "That response is not yours.");

    public static readonly Error NotModerator = Error.Forbidden(
        CommunityLifeErrorCodes.NotModerator,
        "That is a moderator action.");

    public static readonly Error NotProviderAgent = Error.Forbidden(
        CommunityLifeErrorCodes.NotProviderAgent,
        "You cannot act on behalf of that provider.");

    public static readonly Error InsufficientMembershipRole = Error.Forbidden(
        CommunityLifeErrorCodes.InsufficientMembershipRole,
        "Your role in that organization does not allow that.");

    // ── Organization lifecycle ──────────────────────────────────────────────────────────────

    public static readonly Error OrganizationTransitionInvalid = Error.Conflict(
        CommunityLifeErrorCodes.OrganizationTransitionInvalid,
        "That organization cannot move to that state from where it is.");

    public static readonly Error OrganizationAlreadyPublished = Error.Conflict(
        CommunityLifeErrorCodes.OrganizationAlreadyPublished,
        "That organization is already published.");

    public static readonly Error OrganizationNotPublishable = Error.Validation(
        CommunityLifeErrorCodes.OrganizationNotPublishable,
        "Add a description, a type and a place before publishing.");

    public static readonly Error OrganizationNotArchived = Error.Conflict(
        CommunityLifeErrorCodes.OrganizationNotArchived,
        "That organization is not archived.");

    public static readonly Error OrganizationArchivedImmutable = Error.Conflict(
        CommunityLifeErrorCodes.OrganizationArchivedImmutable,
        "Restore that organization before editing it.");

    public static readonly Error OrganizationSuspended = Error.Forbidden(
        CommunityLifeErrorCodes.OrganizationSuspended,
        "That organization is suspended.");

    /// <summary>
    /// The same answer for a community that does not exist and one the host may not post in, so a
    /// refusal never confirms that a private community is there.
    /// </summary>
    public static readonly Error CommunityPostingForbidden = Error.Forbidden(
        CommunityLifeErrorCodes.CommunityPostingForbidden,
        "You can only post an event in a community you are a member of, and only while it is active.");

    public static readonly Error DuplicateOrganization = Error.Conflict(
        CommunityLifeErrorCodes.DuplicateOrganization,
        "You already run an organization with that name.");

    public static readonly Error TooManyOrganizations = Error.Conflict(
        CommunityLifeErrorCodes.TooManyOrganizations,
        "You are already running as many organizations as we allow.");

    public static readonly Error OwnerCannotLeave = Error.Conflict(
        CommunityLifeErrorCodes.OwnerCannotLeave,
        "Hand the organization to somebody else before leaving it.");

    // ── Event lifecycle ─────────────────────────────────────────────────────────────────────

    public static readonly Error EventTransitionInvalid = Error.Conflict(
        CommunityLifeErrorCodes.EventTransitionInvalid,
        "That event cannot move to that state from where it is.");

    public static readonly Error EventAlreadyPublished = Error.Conflict(
        CommunityLifeErrorCodes.EventAlreadyPublished,
        "That event is already published.");

    public static readonly Error EventNotPublishable = Error.Validation(
        CommunityLifeErrorCodes.EventNotPublishable,
        "Add a description, a category, a time and a place before publishing.");

    public static readonly Error EventNotArchived = Error.Conflict(
        CommunityLifeErrorCodes.EventNotArchived,
        "That event is not archived.");

    public static readonly Error EventArchivedImmutable = Error.Conflict(
        CommunityLifeErrorCodes.EventArchivedImmutable,
        "Restore that event before editing it.");

    public static readonly Error EventCancelled = Error.Conflict(
        CommunityLifeErrorCodes.EventCancelled,
        "That event has been cancelled.");

    public static readonly Error EventCompleted = Error.Conflict(
        CommunityLifeErrorCodes.EventCompleted,
        "That event is over.");

    public static readonly Error DuplicateEvent = Error.Conflict(
        CommunityLifeErrorCodes.DuplicateEvent,
        "You have already created that event.");

    public static readonly Error TooManyEvents = Error.Conflict(
        CommunityLifeErrorCodes.TooManyEvents,
        "You have as many live events as we allow.");

    // ── Event content ───────────────────────────────────────────────────────────────────────

    public static readonly Error TitleInvalid = Error.Validation(
        CommunityLifeErrorCodes.TitleInvalid,
        "Give it a title between 3 and 200 characters.");

    public static readonly Error DescriptionInvalid = Error.Validation(
        CommunityLifeErrorCodes.DescriptionInvalid,
        "Say something about it. A short summary is required.");

    public static readonly Error SlugInvalid = Error.Validation(
        CommunityLifeErrorCodes.SlugInvalid,
        "That link handle cannot be used.");

    public static readonly Error ScheduleInvalid = Error.Validation(
        CommunityLifeErrorCodes.ScheduleInvalid,
        "The end time has to be after the start time.");

    public static readonly Error ScheduleInPast = Error.Validation(
        CommunityLifeErrorCodes.ScheduleInPast,
        "That event would already be over.");

    public static readonly Error ScheduleTooFarAhead = Error.Validation(
        CommunityLifeErrorCodes.ScheduleTooFarAhead,
        "That date is too far in the future. Check the year.");

    public static readonly Error ScheduleTooLong = Error.Validation(
        CommunityLifeErrorCodes.ScheduleTooLong,
        "A single event cannot run for longer than a month.");

    public static readonly Error TimeZoneInvalid = Error.Validation(
        CommunityLifeErrorCodes.TimeZoneInvalid,
        "We do not recognise that time zone.");

    public static readonly Error VenueRequired = Error.Validation(
        CommunityLifeErrorCodes.VenueRequired,
        "An event people attend in person needs somewhere to be.");

    public static readonly Error VenueNotPermitted = Error.Validation(
        CommunityLifeErrorCodes.VenueNotPermitted,
        "An online-only event has no venue.");

    public static readonly Error CapacityInvalid = Error.Validation(
        CommunityLifeErrorCodes.CapacityInvalid,
        "A capacity has to be at least one.");

    public static readonly Error CapacityBelowCommitted = Error.Conflict(
        CommunityLifeErrorCodes.CapacityBelowCommitted,
        "More people are already coming than that.");

    public static readonly Error RegistrationDeadlineInvalid = Error.Validation(
        CommunityLifeErrorCodes.RegistrationDeadlineInvalid,
        "The registration deadline has to fall before the event ends.");

    public static readonly Error AgendaLimit = Error.Validation(
        CommunityLifeErrorCodes.AgendaLimit,
        "That is more agenda items than one event can carry.");

    public static readonly Error SpeakerLimit = Error.Validation(
        CommunityLifeErrorCodes.SpeakerLimit,
        "That is more speakers than one event can carry.");

    public static readonly Error LinkedResourceLimit = Error.Validation(
        CommunityLifeErrorCodes.LinkedResourceLimit,
        "That is more linked items than one event can carry.");

    public static readonly Error LinkedResourceInvalid = Error.Validation(
        CommunityLifeErrorCodes.LinkedResourceInvalid,
        "That linked item cannot be attached.");

    public static readonly Error EventCategoryInvalid = Error.Validation(
        CommunityLifeErrorCodes.EventCategoryInvalid,
        "Pick a kind of event from the list.");

    // ── Organization content ────────────────────────────────────────────────────────────────

    public static readonly Error OrganizationNameInvalid = Error.Validation(
        CommunityLifeErrorCodes.OrganizationNameInvalid,
        "Give it a name between 3 and 200 characters.");

    public static readonly Error OrganizationTypeInvalid = Error.Validation(
        CommunityLifeErrorCodes.OrganizationTypeInvalid,
        "Pick a kind of organization from the list.");

    public static readonly Error ContactInvalid = Error.Validation(
        CommunityLifeErrorCodes.ContactInvalid,
        "Those contact details do not look right.");

    public static readonly Error SocialLinkInvalid = Error.Validation(
        CommunityLifeErrorCodes.SocialLinkInvalid,
        "That link does not look right.");

    public static readonly Error OperatingHoursInvalid = Error.Validation(
        CommunityLifeErrorCodes.OperatingHoursInvalid,
        "Those opening hours do not make sense.");

    public static readonly Error OperatingHoursLimit = Error.Validation(
        CommunityLifeErrorCodes.OperatingHoursLimit,
        "That is more opening-hour rows than a week can hold.");

    public static readonly Error LeaderLimit = Error.Validation(
        CommunityLifeErrorCodes.LeaderLimit,
        "That is more named leaders than a page can carry.");

    public static readonly Error LanguageLimit = Error.Validation(
        CommunityLifeErrorCodes.LanguageLimit,
        "That is more languages than a page can carry.");

    public static readonly Error ServiceLimit = Error.Validation(
        CommunityLifeErrorCodes.ServiceLimit,
        "That is more services than a page can carry.");

    // ── Membership ──────────────────────────────────────────────────────────────────────────

    public static readonly Error MembershipTransitionInvalid = Error.Conflict(
        CommunityLifeErrorCodes.MembershipTransitionInvalid,
        "That membership cannot move to that state from where it is.");

    public static readonly Error AlreadyMember = Error.Conflict(
        CommunityLifeErrorCodes.AlreadyMember,
        "You are already a member.");

    public static readonly Error MembershipClosed = Error.Conflict(
        CommunityLifeErrorCodes.MembershipClosed,
        "That organization is not taking members at the moment.");

    public static readonly Error MembershipInviteOnly = Error.Forbidden(
        CommunityLifeErrorCodes.MembershipInviteOnly,
        "That organization admits members by invitation.");

    public static readonly Error MembershipRemoved = Error.Forbidden(
        CommunityLifeErrorCodes.MembershipRemoved,
        "An administrator removed you from that organization.");

    public static readonly Error MembershipNotPending = Error.Conflict(
        CommunityLifeErrorCodes.MembershipNotPending,
        "That membership is not waiting on a decision.");

    public static readonly Error MembershipRequirementsNotMet = Error.Validation(
        CommunityLifeErrorCodes.MembershipRequirementsNotMet,
        "You do not meet that organization's membership requirements.");

    public static readonly Error MembershipPolicyInvalid = Error.Validation(
        CommunityLifeErrorCodes.MembershipPolicyInvalid,
        "That membership policy does not make sense.");

    /// <summary>
    /// A cap on requests waiting on somebody else, not on memberships.
    /// <para>
    /// Belonging to twenty bodies is ordinary in a diaspora. Twenty applications nobody has answered
    /// is the shape a nuisance takes, and every one of them sits in an administrator's queue.
    /// </para>
    /// </summary>
    public static readonly Error TooManyMembershipRequests = Error.Conflict(
        CommunityLifeErrorCodes.TooManyMembershipRequests,
        "You have too many membership requests waiting. Wait for one to be answered first.");

    /// <summary>
    /// Deleting an event people are still coming to.
    /// <para>
    /// Refused rather than cascaded: cancelling tells the attendees and releases their places,
    /// whereas a delete would leave a dozen people planning to turn up somewhere that no longer
    /// exists and nothing on the platform saying otherwise.
    /// </para>
    /// </summary>
    public static readonly Error EventHasAttendees = Error.Conflict(
        CommunityLifeErrorCodes.EventHasAttendees,
        "People are still coming to that event. Cancel it instead, so they are told.");

    /// <summary>
    /// A cap on live responses per member, not on attendance.
    /// <para>
    /// Counted over upcoming events only, so somebody who has been to two hundred things over five
    /// years is not stopped from answering the next one.
    /// </para>
    /// </summary>
    public static readonly Error TooManyRsvps = Error.Conflict(
        CommunityLifeErrorCodes.TooManyRsvps,
        "You are down for too many upcoming events. Withdraw from one first.");

    /// <summary>
    /// A guest list the organizer keeps closed.
    /// <para>
    /// Forbidden rather than not-found: the event is plainly visible, and pretending its attendee
    /// list does not exist would only confuse somebody who can see the event it belongs to.
    /// </para>
    /// </summary>
    public static readonly Error AttendeeListPrivate = Error.Forbidden(
        CommunityLifeErrorCodes.AttendeeListPrivate,
        "The organizer keeps this event's guest list private.");

    public static readonly Error LastOwner = Error.Conflict(
        CommunityLifeErrorCodes.LastOwner,
        "An organization needs an owner. Name another one first.");

    public static readonly Error RoleNotAssignable = Error.Forbidden(
        CommunityLifeErrorCodes.RoleNotAssignable,
        "You cannot grant a role above your own.");

    // ── RSVP & check-in ─────────────────────────────────────────────────────────────────────

    public static readonly Error RsvpTransitionInvalid = Error.Conflict(
        CommunityLifeErrorCodes.RsvpTransitionInvalid,
        "That response cannot change in that way.");

    public static readonly Error RsvpNotRequired = Error.Validation(
        CommunityLifeErrorCodes.RsvpNotRequired,
        "That event does not take responses. Just turn up.");

    public static readonly Error RsvpClosed = Error.Conflict(
        CommunityLifeErrorCodes.RsvpClosed,
        "That event is not taking responses.");

    public static readonly Error RsvpDeadlinePassed = Error.Conflict(
        CommunityLifeErrorCodes.RsvpDeadlinePassed,
        "Registration for that event has closed.");

    public static readonly Error EventFull = Error.Conflict(
        CommunityLifeErrorCodes.EventFull,
        "That event is full.");

    public static readonly Error AlreadyRsvped = Error.Conflict(
        CommunityLifeErrorCodes.AlreadyRsvped,
        "You have already responded to that event.");

    public static readonly Error CheckInNotOpen = Error.Conflict(
        CommunityLifeErrorCodes.CheckInNotOpen,
        "Check-in is not open for that event.");

    public static readonly Error CheckInMethodUnavailable = Error.Validation(
        CommunityLifeErrorCodes.CheckInMethodUnavailable,
        "That way of checking in is not available here.");

    public static readonly Error AlreadyCheckedIn = Error.Conflict(
        CommunityLifeErrorCodes.AlreadyCheckedIn,
        "They are already checked in.");

    public static readonly Error NotAttending = Error.Conflict(
        CommunityLifeErrorCodes.NotAttending,
        "They did not say they were coming.");

    /// <summary>
    /// Deliberately a forbidden rather than a not-found. See the class summary.
    /// </summary>
    public static readonly Error EventNotMemberVisible = Error.Forbidden(
        CommunityLifeErrorCodes.EventNotMemberVisible,
        "That event is for members of the organization.");

    // ── Media ───────────────────────────────────────────────────────────────────────────────

    public static readonly Error MediaInvalid = Error.Validation(
        CommunityLifeErrorCodes.MediaInvalid,
        "That media reference is not valid.");

    public static readonly Error MediaLimit = Error.Validation(
        CommunityLifeErrorCodes.MediaLimit,
        "That is more media than one page can carry.");

    public static readonly Error GuideReferenceInvalid = Error.Validation(
        CommunityLifeErrorCodes.GuideReferenceInvalid,
        "That guide cannot be attached.");

    public static readonly Error ProviderReferenceInvalid = Error.Validation(
        CommunityLifeErrorCodes.ProviderReferenceInvalid,
        "That provider cannot be attached.");

    // ── Moderation & trust ──────────────────────────────────────────────────────────────────

    public static readonly Error AlreadyReported = Error.Conflict(
        CommunityLifeErrorCodes.AlreadyReported,
        "You have already reported that.");

    public static readonly Error CannotReportOwn = Error.Validation(
        CommunityLifeErrorCodes.CannotReportOwn,
        "You cannot report your own event or organization.");

    public static readonly Error ReportSubjectInvalid = Error.Validation(
        CommunityLifeErrorCodes.ReportSubjectInvalid,
        "Only an event or an organization can be reported.");

    public static readonly Error ReportReasonInvalid = Error.Validation(
        CommunityLifeErrorCodes.ReportReasonInvalid,
        "That is not a reason this platform recognises.");

    public static readonly Error ModerationActionInvalid = Error.Validation(
        CommunityLifeErrorCodes.ModerationActionInvalid,
        "That is not a moderation action.");

    public static readonly Error ReportAlreadyResolved = Error.Conflict(
        CommunityLifeErrorCodes.ReportAlreadyResolved,
        "That report has already been decided.");

    public static readonly Error SpamHold = Error.Conflict(
        CommunityLifeErrorCodes.SpamHold,
        "That event has been held for review.");

    public static readonly Error VerificationAlreadyPending = Error.Conflict(
        CommunityLifeErrorCodes.VerificationAlreadyPending,
        "A verification request is already open for that organization.");

    public static readonly Error VerificationUnavailable = Error.ServiceUnavailable(
        CommunityLifeErrorCodes.VerificationUnavailable,
        "Verification is not available at the moment.");

    public static readonly Error VerificationRefused = Error.Validation(
        CommunityLifeErrorCodes.VerificationRefused,
        "That verification request was refused.");

    // ── Recurring series & occurrences ──────────────────────────────────────────────────────

    public static readonly Error EventSeriesNotFound = Error.NotFound(
        CommunityLifeErrorCodes.EventSeriesNotFound,
        "That repeating event does not exist, or you cannot see it.");

    public static readonly Error EventSeriesTransitionInvalid = Error.Conflict(
        CommunityLifeErrorCodes.EventSeriesTransitionInvalid,
        "That repeating event cannot move to that state from where it is.");

    public static readonly Error EventSeriesTerminal = Error.Conflict(
        CommunityLifeErrorCodes.EventSeriesTerminal,
        "That repeating event has finished or been called off.");

    public static readonly Error SeriesExceptionLimit = Error.Conflict(
        CommunityLifeErrorCodes.SeriesExceptionLimit,
        "Too many dates have been removed from that repeating event.");

    public static readonly Error SeriesOccurrenceLimit = Error.Conflict(
        CommunityLifeErrorCodes.SeriesOccurrenceLimit,
        "That repeating event already has as many dates as we allow.");

    /// <summary>
    /// A scope of <c>ThisOccurrence</c> or <c>ThisAndFuture</c> with no occurrence named.
    /// <para>
    /// Refused rather than defaulted to the whole series: the difference between the three scopes is
    /// the difference between moving one evening and moving a hundred, and guessing would move them.
    /// </para>
    /// </summary>
    public static readonly Error SeriesScopeInvalid = Error.Validation(
        CommunityLifeErrorCodes.SeriesScopeInvalid,
        "Say which date the change applies to.");

    public static readonly Error OccurrenceNotFound = Error.NotFound(
        CommunityLifeErrorCodes.OccurrenceNotFound,
        "That date does not exist, or you cannot see it.");

    public static readonly Error OccurrenceNotMovable = Error.Conflict(
        CommunityLifeErrorCodes.OccurrenceNotMovable,
        "That date has been called off or is already over.");

    public static readonly Error RecurrenceIntervalInvalid = Error.Validation(
        CommunityLifeErrorCodes.RecurrenceIntervalInvalid,
        "How often it repeats has to be between 1 and 52.");

    public static readonly Error RecurrenceWeekdayInvalid = Error.Validation(
        CommunityLifeErrorCodes.RecurrenceWeekdayInvalid,
        "Weekdays are numbered 1 for Monday through 7 for Sunday.");

    public static readonly Error RecurrenceMonthDayInvalid = Error.Validation(
        CommunityLifeErrorCodes.RecurrenceMonthDayInvalid,
        "Days of the month run from 1 to 31, or -1 to -31 counting from the end.");

    public static readonly Error RecurrenceEndInvalid = Error.Validation(
        CommunityLifeErrorCodes.RecurrenceEndInvalid,
        "Give the date it stops repeating on.");

    public static readonly Error RecurrenceCountInvalid = Error.Validation(
        CommunityLifeErrorCodes.RecurrenceCountInvalid,
        "Give how many times it repeats, up to 500.");

    public static readonly Error RecurrenceFrequencyInvalid = Error.Validation(
        CommunityLifeErrorCodes.RecurrenceFrequencyInvalid,
        "It can repeat daily, weekly, monthly or yearly.");

    /// <summary>
    /// A legal rule that generates no date at all — the 31st of February, or an end date before the
    /// start. Refused at the write, because a series that will never produce anything looks exactly
    /// like one whose horizon has not caught up yet.
    /// </summary>
    public static readonly Error RecurrenceProducesNothing = Error.Validation(
        CommunityLifeErrorCodes.RecurrenceProducesNothing,
        "That pattern never falls on a real date. Check the day and the end date.");

    // ── Concurrency & paging ────────────────────────────────────────────────────────────────

    public static readonly Error RevisionConflict = Error.Conflict(
        CommunityLifeErrorCodes.RevisionConflict,
        "Somebody else changed this while you were editing. Reload and try again.");

    public static readonly Error CursorInvalid = Error.Validation(
        CommunityLifeErrorCodes.CursorInvalid,
        "That page marker is not valid.");

    public static readonly Error CatalogueEntryExists = Error.Conflict(
        CommunityLifeErrorCodes.CatalogueEntryExists,
        "That catalogue entry already exists.");
}
