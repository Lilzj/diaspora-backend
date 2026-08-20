namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// The stable wire-level error codes the Community Programme module emits.
/// <para>
/// These strings are frozen contract: clients switch on them, support searches logs by them, and
/// translations key off them. Change the description on an <c>Error</c> freely; never change a code.
/// New failures get new codes.
/// </para>
/// </summary>
public static class CommunityProgrammeErrorCodes
{
    private const string Prefix = "communities";

    // ── Existence ───────────────────────────────────────────────────────────────────────────

    public const string OrganizationNotFound = Prefix + ".organization.not-found";
    public const string ProgrammeNotFound = Prefix + ".membership-programme.not-found";
    public const string TierNotFound = Prefix + ".membership-tier.not-found";
    public const string MembershipNotFound = Prefix + ".membership.not-found";
    public const string OpportunityNotFound = Prefix + ".volunteer-opportunity.not-found";
    public const string ShiftNotFound = Prefix + ".volunteer-shift.not-found";
    public const string ApplicationNotFound = Prefix + ".volunteer-application.not-found";
    public const string AssignmentNotFound = Prefix + ".volunteer-assignment.not-found";
    public const string PollNotFound = Prefix + ".poll.not-found";
    public const string PollOptionNotFound = Prefix + ".poll-option.not-found";
    public const string AnnouncementNotFound = Prefix + ".announcement.not-found";

    // ── Authorization ───────────────────────────────────────────────────────────────────────

    public const string NotAuthenticated = Prefix + ".not-authenticated";

    /// <summary>The caller holds no role in the organization that would permit the act.</summary>
    public const string CapabilityRequired = Prefix + ".organization.capability-required";

    /// <summary>The caller does not own the membership, application or assignment named.</summary>
    public const string NotOwner = Prefix + ".not-owner";

    /// <summary>The caller is outside the poll's audience.</summary>
    public const string OutsideAudience = Prefix + ".poll.outside-audience";

    // ── Membership lifecycle ────────────────────────────────────────────────────────────────

    public const string MembershipTransitionInvalid = Prefix + ".membership.transition-invalid";
    public const string MembershipAlreadyExists = Prefix + ".membership.already-exists";
    public const string ProgrammeClosed = Prefix + ".membership-programme.closed";
    public const string TierFull = Prefix + ".membership-tier.full";
    public const string RequirementsNotMet = Prefix + ".membership.requirements-not-met";
    public const string MembershipNotRenewable = Prefix + ".membership.not-renewable";
    public const string ProgrammeAlreadyExists = Prefix + ".membership-programme.already-exists";

    // ── Volunteering ────────────────────────────────────────────────────────────────────────

    public const string OpportunityNotOpen = Prefix + ".volunteer-opportunity.not-open";
    public const string OpportunityTransitionInvalid =
        Prefix + ".volunteer-opportunity.transition-invalid";
    public const string ApplicationTransitionInvalid =
        Prefix + ".volunteer-application.transition-invalid";
    public const string AlreadyApplied = Prefix + ".volunteer-application.already-applied";
    public const string ApplicationNotApproved = Prefix + ".volunteer-application.not-approved";
    public const string AlreadyAssigned = Prefix + ".volunteer-assignment.already-assigned";
    public const string AssignmentTransitionInvalid =
        Prefix + ".volunteer-assignment.transition-invalid";
    public const string ShiftFull = Prefix + ".volunteer-shift.full";
    public const string HoursNotConfirmable = Prefix + ".volunteer-assignment.hours-not-confirmable";
    public const string ApplicationDeadlinePassed =
        Prefix + ".volunteer-opportunity.deadline-passed";
    public const string BelowMinimumAge = Prefix + ".volunteer-opportunity.below-minimum-age";

    // ── Polls ───────────────────────────────────────────────────────────────────────────────

    public const string PollTransitionInvalid = Prefix + ".poll.transition-invalid";
    public const string PollNotOpen = Prefix + ".poll.not-open";
    public const string AlreadyVoted = Prefix + ".poll.already-voted";
    public const string TooManySelections = Prefix + ".poll.too-many-selections";
    public const string SelectionRequired = Prefix + ".poll.selection-required";
    public const string OpenResponseRequired = Prefix + ".poll.open-response-required";

    // ── Announcements ───────────────────────────────────────────────────────────────────────

    public const string AnnouncementScopeInvalid = Prefix + ".announcement.scope-invalid";

    // ── Validation ──────────────────────────────────────────────────────────────────────────

    public const string CurrencyNotSupported = Prefix + ".currency.not-supported";
    public const string ValidationFailed = Prefix + ".validation-failed";
    public const string PeriodInvalid = Prefix + ".period.invalid";

    // ── Idempotency ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A key was replayed while its first request was still committing.
    /// <para>
    /// <b>409, not 500, and not a second write.</b> The unique index refused the duplicate, the
    /// handler re-read by key and still found nothing, so the original transaction has not committed
    /// yet. The client's own retry policy treats this as retryable, which is exactly right: a moment
    /// later the read will find the first request's row and replay it.
    /// </para>
    /// </summary>
    public const string IdempotencyKeyInFlight = Prefix + ".idempotency.in-flight";
}
