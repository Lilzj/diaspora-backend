using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Errors;

/// <summary>
/// The module's failures, declared once.
/// <para>
/// Declared here rather than constructed at the call site so a code and its description stay
/// together, and so the wire contract cannot drift when two handlers report the same refusal.
/// </para>
/// <para>
/// <b>"Not yours" and "does not exist" are the same error throughout.</b> A caller probing for a
/// creator who has opted out of discovery, an unpublished grant or somebody else's application
/// learns nothing from a 404 that they did not already know. The exception is a route where the
/// caller demonstrably holds the resource and lacks a capability — there a 403 is the honest
/// answer.
/// </para>
/// <para>
/// <b>Nothing here explains a risk refusal.</b> §13 of the phase brief and the register agree:
/// "The <c>Blocked</c> copy is deliberately vague — no mention of the check that was tripped. A
/// fraud refusal that explains itself is a map for getting round it." The member-safe sentences
/// below are the whole of what the platform will say.
/// </para>
/// </summary>
public static class CreatorErrors
{
    // ── Existence ───────────────────────────────────────────────────────────────────────────

    public static readonly Error CreatorNotFound = Error.NotFound(
        CreatorErrorCodes.CreatorNotFound, "There is no such creator.");

    public static readonly Error MilestoneNotFound = Error.NotFound(
        CreatorErrorCodes.MilestoneNotFound, "There is no such milestone.");

    public static readonly Error ChallengeNotFound = Error.NotFound(
        CreatorErrorCodes.ChallengeNotFound, "There is no such challenge.");

    public static readonly Error GrantNotFound = Error.NotFound(
        CreatorErrorCodes.GrantNotFound, "There is no such grant.");

    public static readonly Error GrantApplicationNotFound = Error.NotFound(
        CreatorErrorCodes.GrantApplicationNotFound, "There is no such application.");

    public static readonly Error CourseNotFound = Error.NotFound(
        CreatorErrorCodes.CourseNotFound, "There is no such course.");

    public static readonly Error LessonNotFound = Error.NotFound(
        CreatorErrorCodes.LessonNotFound, "There is no such lesson.");

    public static readonly Error QuizNotFound = Error.NotFound(
        CreatorErrorCodes.QuizNotFound, "There is no such quiz.");

    public static readonly Error RewardNotFound = Error.NotFound(
        CreatorErrorCodes.RewardNotFound, "There is no such reward.");

    public static readonly Error VerificationNotFound = Error.NotFound(
        CreatorErrorCodes.VerificationNotFound,
        "You have not started a creator verification application.");

    // ── Authorization ───────────────────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        CreatorErrorCodes.NotAuthenticated, "Sign in to do that.");

    public static readonly Error NotOwner = Error.Forbidden(
        CreatorErrorCodes.NotOwner, "That is not yours.");

    /// <summary>
    /// §30: an applicant cannot decide their own application. Separately checked from the
    /// reviewer permission, because holding the permission and being the applicant are both true
    /// for a member of staff who applied for a grant.
    /// </summary>
    public static readonly Error ReviewerCannotDecideOwn = Error.Forbidden(
        CreatorErrorCodes.ReviewerCannotDecideOwn,
        "You cannot decide an application you made.");

    public static readonly Error NotAReviewer = Error.Forbidden(
        CreatorErrorCodes.NotAReviewer, "You are not a reviewer for this.");

    // ── Standing ────────────────────────────────────────────────────────────────────────────

    public static readonly Error CreatorRestricted = Error.Forbidden(
        CreatorErrorCodes.CreatorRestricted,
        "Your creator account is restricted, so this cannot be changed at the moment.");

    public static readonly Error CreatorSuspended = Error.Forbidden(
        CreatorErrorCodes.CreatorSuspended, "Your creator account is suspended.");

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public static readonly Error VerificationNotEligible = Error.Conflict(
        CreatorErrorCodes.VerificationNotEligible,
        "You are not eligible for creator verification yet.");

    public static readonly Error VerificationInvalidTransition = Error.Conflict(
        CreatorErrorCodes.VerificationInvalidTransition,
        "That is not something this application can do from where it is.");

    public static readonly Error VerificationRequirementsUnmet = Error.Conflict(
        CreatorErrorCodes.VerificationRequirementsUnmet,
        "Some of the programme's requirements are still outstanding.");

    public static readonly Error VerificationCannotAppeal = Error.Conflict(
        CreatorErrorCodes.VerificationCannotAppeal, "There is nothing to appeal.");

    public static readonly Error EvidenceLimitReached = Error.Conflict(
        CreatorErrorCodes.EvidenceLimitReached,
        "This application already carries as much supporting material as it can.");

    public static readonly Error EvidenceKindUnknown = Error.Validation(
        CreatorErrorCodes.EvidenceKindUnknown, "That is not a kind of evidence this programme accepts.");

    // ── Milestones and challenges ───────────────────────────────────────────────────────────

    public static readonly Error MilestoneNotComplete = Error.Conflict(
        CreatorErrorCodes.MilestoneNotComplete, "That milestone is not complete yet.");

    public static readonly Error MilestoneAlreadyClaimed = Error.Conflict(
        CreatorErrorCodes.MilestoneAlreadyClaimed, "You have already claimed that milestone.");

    public static readonly Error MilestoneExpired = Error.Conflict(
        CreatorErrorCodes.MilestoneExpired, "That milestone has expired.");

    public static readonly Error ChallengeNotOpen = Error.Conflict(
        CreatorErrorCodes.ChallengeNotOpen, "That challenge is not open to join.");

    public static readonly Error ChallengeNotEligible = Error.Conflict(
        CreatorErrorCodes.ChallengeNotEligible, "You are not eligible for that challenge.");

    public static readonly Error ChallengeNotJoined = Error.Conflict(
        CreatorErrorCodes.ChallengeNotJoined, "You have not joined that challenge.");

    public static readonly Error ChallengeNotComplete = Error.Conflict(
        CreatorErrorCodes.ChallengeNotComplete, "That challenge is not complete yet.");

    public static readonly Error ChallengeAlreadyClaimed = Error.Conflict(
        CreatorErrorCodes.ChallengeAlreadyClaimed, "You have already claimed that challenge.");

    // ── Grants ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error GrantNotOpen = Error.Conflict(
        CreatorErrorCodes.GrantNotOpen, "That grant is not accepting applications.");

    public static readonly Error GrantNotEligible = Error.Conflict(
        CreatorErrorCodes.GrantNotEligible, "You are not eligible for that grant.");

    public static readonly Error GrantAlreadyApplied = Error.Conflict(
        CreatorErrorCodes.GrantAlreadyApplied, "You already have an application for that grant.");

    public static readonly Error GrantFieldRequired = Error.Validation(
        CreatorErrorCodes.GrantFieldRequired, "One of the form's required questions is unanswered.");

    public static readonly Error GrantFieldInvalid = Error.Validation(
        CreatorErrorCodes.GrantFieldInvalid, "One of your answers is not in the form the question asks for.");

    public static readonly Error GrantDeclarationRequired = Error.Validation(
        CreatorErrorCodes.GrantDeclarationRequired, "Every declaration has to be accepted.");

    public static readonly Error GrantAmountOutOfRange = Error.Validation(
        CreatorErrorCodes.GrantAmountOutOfRange, "That is outside what this grant awards.");

    public static readonly Error GrantApplicationNotEditable = Error.Conflict(
        CreatorErrorCodes.GrantApplicationNotEditable, "This application can no longer be edited.");

    public static readonly Error GrantApplicationInvalidTransition = Error.Conflict(
        CreatorErrorCodes.GrantApplicationInvalidTransition,
        "That is not something this application can do from where it is.");

    public static readonly Error GrantReportNotDue = Error.Conflict(
        CreatorErrorCodes.GrantReportNotDue, "That report is not one this grant is waiting for.");

    // ── Academy ─────────────────────────────────────────────────────────────────────────────

    /// <summary>The register's own code and the client's own handling. See the code's summary.</summary>
    public static readonly Error LessonQuizRequired = Error.Conflict(
        CreatorErrorCodes.LessonQuizRequired,
        "This lesson is finished by passing its quiz.");

    public static readonly Error QuizRetryNotAvailable = Error.Conflict(
        CreatorErrorCodes.QuizRetryNotAvailable, "You cannot retake this quiz yet.");

    public static readonly Error QuizAnswerUnknown = Error.Validation(
        CreatorErrorCodes.QuizAnswerUnknown, "One of those answers is not an option on this quiz.");

    public static readonly Error CoursePrerequisiteUnmet = Error.Conflict(
        CreatorErrorCodes.CoursePrerequisiteUnmet, "Finish this course's prerequisites first.");

    // ── Referrals and the sign-up bonus ─────────────────────────────────────────────────────

    public static readonly Error ReferralSelf = Error.Conflict(
        CreatorErrorCodes.ReferralSelf, "You cannot refer yourself.");

    public static readonly Error ReferralDuplicate = Error.Conflict(
        CreatorErrorCodes.ReferralDuplicate, "That person has already been referred.");

    public static readonly Error ReferralNotEligible = Error.Conflict(
        CreatorErrorCodes.ReferralNotEligible,
        "The referral programme is not available on your account at the moment.");

    /// <summary>
    /// <b>Deliberately uninformative, and asserted so by a test.</b> The register: a fraud refusal
    /// that explains itself is a map for getting round it.
    /// </summary>
    public static readonly Error SignupBonusNotEligible = Error.Conflict(
        CreatorErrorCodes.SignupBonusNotEligible,
        "This bonus is not available on your account.");

    // ── Rewards ─────────────────────────────────────────────────────────────────────────────

    public static readonly Error RewardNotAvailable = Error.Conflict(
        CreatorErrorCodes.RewardNotAvailable, "That reward is not available to you.");

    public static readonly Error RewardInsufficientPoints = Error.Conflict(
        CreatorErrorCodes.RewardInsufficientPoints, "That is more points than you have.");

    public static readonly Error RewardLimitReached = Error.Conflict(
        CreatorErrorCodes.RewardLimitReached, "You have redeemed that as many times as you can.");

    public static readonly Error RewardCooldown = Error.Conflict(
        CreatorErrorCodes.RewardCooldown, "You have redeemed that recently. Try again later.");

    public static readonly Error RewardVersionStale = Error.Conflict(
        CreatorErrorCodes.RewardVersionStale,
        "The reward catalogue has changed since you looked. Check the new terms.");

    public static readonly Error RewardPointsUnavailable = Error.Conflict(
        CreatorErrorCodes.RewardPointsUnavailable,
        "Your points are not available to spend at the moment.");

    // ── Directory and leaderboard ───────────────────────────────────────────────────────────

    public static readonly Error LeaderboardScopeUnknown = Error.Validation(
        CreatorErrorCodes.LeaderboardScopeUnknown, "That is not a leaderboard this platform keeps.");

    public static readonly Error LeaderboardScopeNeedsSubject = Error.Validation(
        CreatorErrorCodes.LeaderboardScopeNeedsSubject,
        "That leaderboard needs to know which community or challenge you mean.");

    // ── Idempotency ─────────────────────────────────────────────────────────────────────────

    public static readonly Error IdempotencyKeyInFlight = Error.Conflict(
        CreatorErrorCodes.IdempotencyKeyInFlight,
        "An identical request is still being processed. Try again in a moment.");
}
