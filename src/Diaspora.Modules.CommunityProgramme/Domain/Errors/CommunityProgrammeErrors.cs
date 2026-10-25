using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Errors;

/// <summary>
/// The module's failures, declared once.
/// <para>
/// Declared here rather than constructed at the call site so a code and its description stay
/// together, and so the wire contract cannot drift when two handlers report the same refusal.
/// </para>
/// <para>
/// <b>"Not yours" and "does not exist" are the same error throughout.</b> A caller probing for a
/// private programme, an unpublished opportunity or a poll outside their audience learns nothing
/// from a 404 that they did not already know. The exception is a route where the caller demonstrably
/// holds the resource and lacks a capability — there a 403 is the honest answer and is what the
/// register asks for.
/// </para>
/// </summary>
public static class CommunityProgrammeErrors
{
    // ── Existence ───────────────────────────────────────────────────────────────────────────

    public static readonly Error OrganizationNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.OrganizationNotFound, "There is no such organization.");

    public static readonly Error ProgrammeNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.ProgrammeNotFound,
        "This organization does not run a membership programme.");

    public static readonly Error TierNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.TierNotFound, "There is no such membership tier.");

    public static readonly Error MembershipNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.MembershipNotFound, "There is no such membership.");

    public static readonly Error OpportunityNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.OpportunityNotFound,
        "There is no such volunteer opportunity.");

    public static readonly Error ShiftNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.ShiftNotFound, "There is no such shift on this opportunity.");

    public static readonly Error ApplicationNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.ApplicationNotFound, "There is no such application.");

    public static readonly Error AssignmentNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.AssignmentNotFound, "There is no such assignment.");

    public static readonly Error PollNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.PollNotFound, "There is no such poll.");

    public static readonly Error PollOptionNotFound = Error.Validation(
        CommunityProgrammeErrorCodes.PollOptionNotFound, "That is not an option on this poll.");

    public static readonly Error AnnouncementNotFound = Error.NotFound(
        CommunityProgrammeErrorCodes.AnnouncementNotFound, "There is no such announcement.");

    // ── Authorization ───────────────────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        CommunityProgrammeErrorCodes.NotAuthenticated, "Sign in to do that.");

    public static readonly Error NotOwner = Error.Forbidden(
        CommunityProgrammeErrorCodes.NotOwner, "That is not yours.");

    public static readonly Error OutsideAudience = Error.Forbidden(
        CommunityProgrammeErrorCodes.OutsideAudience,
        "This poll is not open to you.");

    /// <summary>
    /// The refusal every role-gated write produces.
    /// <para>
    /// Names the capability rather than the role, because the capability is the contract and the
    /// role is a label. A member told "you need Administrator" learns something that may not even be
    /// true in their organization; one told "you need permission to approve members" can ask for the
    /// right thing.
    /// </para>
    /// </summary>
    public static Error CapabilityRequired(string capability) => Error.Forbidden(
        CommunityProgrammeErrorCodes.CapabilityRequired,
        $"Your role in this organization does not include {capability}.");

    // ── Membership ──────────────────────────────────────────────────────────────────────────

    public static readonly Error MembershipTransitionInvalid = Error.Conflict(
        CommunityProgrammeErrorCodes.MembershipTransitionInvalid,
        "This membership cannot move to that state from where it is.");

    public static readonly Error MembershipAlreadyExists = Error.Conflict(
        CommunityProgrammeErrorCodes.MembershipAlreadyExists,
        "You already hold a membership of this organization.");

    public static readonly Error ProgrammeClosed = Error.Conflict(
        CommunityProgrammeErrorCodes.ProgrammeClosed,
        "This organization is not taking new members at the moment.");

    public static readonly Error TierFull = Error.Conflict(
        CommunityProgrammeErrorCodes.TierFull, "That tier is full.");

    public static readonly Error RequirementsNotMet = Error.Conflict(
        CommunityProgrammeErrorCodes.RequirementsNotMet,
        "You do not meet this tier's requirements yet.");

    public static readonly Error MembershipNotRenewable = Error.Conflict(
        CommunityProgrammeErrorCodes.MembershipNotRenewable,
        "This membership cannot be renewed from where it is.");

    public static readonly Error ProgrammeAlreadyExists = Error.Conflict(
        CommunityProgrammeErrorCodes.ProgrammeAlreadyExists,
        "This organization already runs a membership programme.");

    // ── Volunteering ────────────────────────────────────────────────────────────────────────

    public static readonly Error OpportunityNotOpen = Error.Conflict(
        CommunityProgrammeErrorCodes.OpportunityNotOpen,
        "This opportunity is not taking applications.");

    public static readonly Error OpportunityTransitionInvalid = Error.Conflict(
        CommunityProgrammeErrorCodes.OpportunityTransitionInvalid,
        "This opportunity cannot move to that state from where it is.");

    public static readonly Error ApplicationTransitionInvalid = Error.Conflict(
        CommunityProgrammeErrorCodes.ApplicationTransitionInvalid,
        "This application cannot move to that state from where it is.");

    public static readonly Error AlreadyApplied = Error.Conflict(
        CommunityProgrammeErrorCodes.AlreadyApplied, "You have already applied for this.");

    public static readonly Error ApplicationNotApproved = Error.Conflict(
        CommunityProgrammeErrorCodes.ApplicationNotApproved,
        "Only an approved volunteer can be assigned.");

    public static readonly Error AlreadyAssigned = Error.Conflict(
        CommunityProgrammeErrorCodes.AlreadyAssigned,
        "This volunteer already holds an assignment for that shift.");

    public static readonly Error AssignmentTransitionInvalid = Error.Conflict(
        CommunityProgrammeErrorCodes.AssignmentTransitionInvalid,
        "This assignment cannot move to that state from where it is.");

    public static readonly Error ShiftFull = Error.Conflict(
        CommunityProgrammeErrorCodes.ShiftFull, "That shift is full.");

    public static readonly Error HoursNotConfirmable = Error.Conflict(
        CommunityProgrammeErrorCodes.HoursNotConfirmable,
        "Hours can only be confirmed once the volunteer has been checked in.");

    public static readonly Error ApplicationDeadlinePassed = Error.Conflict(
        CommunityProgrammeErrorCodes.ApplicationDeadlinePassed,
        "The deadline for applying to this opportunity has passed.");

    public static readonly Error BelowMinimumAge = Error.Conflict(
        CommunityProgrammeErrorCodes.BelowMinimumAge,
        "This opportunity has a minimum age you do not meet.");

    // ── Polls ───────────────────────────────────────────────────────────────────────────────

    public static readonly Error PollTransitionInvalid = Error.Conflict(
        CommunityProgrammeErrorCodes.PollTransitionInvalid,
        "This poll cannot move to that state from where it is.");

    public static readonly Error PollNotOpen = Error.Conflict(
        CommunityProgrammeErrorCodes.PollNotOpen, "This poll is not open for voting.");

    public static readonly Error AlreadyVoted = Error.Conflict(
        CommunityProgrammeErrorCodes.AlreadyVoted,
        "You have already voted, and this poll does not allow changes.");

    public static readonly Error TooManySelections = Error.Validation(
        CommunityProgrammeErrorCodes.TooManySelections,
        "You have chosen more options than this poll allows.");

    public static readonly Error SelectionRequired = Error.Validation(
        CommunityProgrammeErrorCodes.SelectionRequired, "Choose an option.");

    public static readonly Error OpenResponseRequired = Error.Validation(
        CommunityProgrammeErrorCodes.OpenResponseRequired, "Write an answer.");

    // ── Announcements ───────────────────────────────────────────────────────────────────────

    public static readonly Error AnnouncementScopeInvalid = Error.Validation(
        CommunityProgrammeErrorCodes.AnnouncementScopeInvalid,
        "An announcement to that scope must name what it is addressed to.");

    // ── Validation ──────────────────────────────────────────────────────────────────────────

    public static Error CurrencyNotSupported(string currency) => Error.Validation(
        CommunityProgrammeErrorCodes.CurrencyNotSupported,
        $"'{currency}' is not a currency this deployment takes.");

    public static readonly Error PeriodInvalid = Error.Validation(
        CommunityProgrammeErrorCodes.PeriodInvalid, "That is not a period this platform reports on.");

    public static Error Validation(string description) => Error.Validation(
        CommunityProgrammeErrorCodes.ValidationFailed, description);

    // ── Idempotency ─────────────────────────────────────────────────────────────────────────

    public static readonly Error IdempotencyKeyInFlight = Error.Conflict(
        CommunityProgrammeErrorCodes.IdempotencyKeyInFlight,
        "An identical request is still being processed. Try again in a moment.");

    public static readonly Error IdempotencyKeyReused = Error.Validation(
        CommunityProgrammeErrorCodes.IdempotencyKeyReused,
        "That idempotency key was already used for a different request. Send a new key.");
}
