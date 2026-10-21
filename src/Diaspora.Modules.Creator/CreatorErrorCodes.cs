namespace Diaspora.Modules.Creator;

/// <summary>
/// The wire codes this module returns.
/// <para>
/// Dotted, module-prefixed, and stable: a client switches on these, so a rename is a breaking
/// change. Declared as constants in one place so a code and the <see cref="Domain.Errors"/> entry
/// that carries it cannot drift.
/// </para>
/// <para>
/// <b>Two of them are the frontend's own, quoted from the register rather than invented here.</b>
/// <see cref="LessonQuizRequired"/> is the refusal the Academy viewer already handles — "POST
/// /lessons/{id}/complete on a quiz lesson is refused with creator.lesson.quiz_required" — and
/// <see cref="IdempotencyKeyInFlight"/> is the shape the client's retry policy already treats as
/// retryable. Choosing a different string for either would have been a client change.
/// </para>
/// </summary>
public static class CreatorErrorCodes
{
    private const string Prefix = "creator.";

    // ── Existence ───────────────────────────────────────────────────────────────────────────

    public const string CreatorNotFound = Prefix + "profile.not-found";
    public const string MilestoneNotFound = Prefix + "milestone.not-found";
    public const string ChallengeNotFound = Prefix + "challenge.not-found";
    public const string GrantNotFound = Prefix + "grant.not-found";
    public const string GrantApplicationNotFound = Prefix + "grant.application.not-found";
    public const string CourseNotFound = Prefix + "course.not-found";
    public const string LessonNotFound = Prefix + "lesson.not-found";
    public const string QuizNotFound = Prefix + "quiz.not-found";
    public const string RewardNotFound = Prefix + "reward.not-found";
    public const string VerificationNotFound = Prefix + "verification.not-found";

    // ── Authorization ───────────────────────────────────────────────────────────────────────

    public const string NotAuthenticated = Prefix + "not-authenticated";
    public const string NotOwner = Prefix + "not-yours";
    public const string ReviewerCannotDecideOwn = Prefix + "review.self";
    public const string NotAReviewer = Prefix + "review.forbidden";

    // ── Standing ────────────────────────────────────────────────────────────────────────────

    public const string CreatorRestricted = Prefix + "profile.restricted";
    public const string CreatorSuspended = Prefix + "profile.suspended";

    // ── Verification ────────────────────────────────────────────────────────────────────────

    public const string VerificationNotEligible = Prefix + "verification.not-eligible";
    public const string VerificationInvalidTransition = Prefix + "verification.invalid-transition";
    public const string VerificationRequirementsUnmet = Prefix + "verification.requirements-unmet";
    public const string VerificationCannotAppeal = Prefix + "verification.cannot-appeal";
    public const string EvidenceLimitReached = Prefix + "verification.evidence-limit";
    public const string EvidenceKindUnknown = Prefix + "verification.evidence-kind";

    // ── Milestones and challenges ───────────────────────────────────────────────────────────

    public const string MilestoneNotComplete = Prefix + "milestone.not-complete";
    public const string MilestoneAlreadyClaimed = Prefix + "milestone.already-claimed";
    public const string MilestoneExpired = Prefix + "milestone.expired";
    public const string ChallengeNotOpen = Prefix + "challenge.not-open";
    public const string ChallengeNotEligible = Prefix + "challenge.not-eligible";
    public const string ChallengeNotJoined = Prefix + "challenge.not-joined";
    public const string ChallengeNotComplete = Prefix + "challenge.not-complete";
    public const string ChallengeAlreadyClaimed = Prefix + "challenge.already-claimed";

    // ── Grants ──────────────────────────────────────────────────────────────────────────────

    public const string GrantNotOpen = Prefix + "grant.not-open";
    public const string GrantNotEligible = Prefix + "grant.not-eligible";
    public const string GrantAlreadyApplied = Prefix + "grant.already-applied";
    public const string GrantFieldRequired = Prefix + "grant.field-required";
    public const string GrantFieldInvalid = Prefix + "grant.field-invalid";
    public const string GrantDeclarationRequired = Prefix + "grant.declaration-required";
    public const string GrantAmountOutOfRange = Prefix + "grant.amount-out-of-range";
    public const string GrantApplicationNotEditable = Prefix + "grant.application.not-editable";
    public const string GrantApplicationInvalidTransition = Prefix + "grant.application.invalid-transition";
    public const string GrantReportNotDue = Prefix + "grant.report.not-due";

    // ── Academy ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The frontend's own code, quoted from the register. Completing a quiz lesson is a graded
    /// submission, never an assertion.
    /// </summary>
    public const string LessonQuizRequired = Prefix + "lesson.quiz_required";

    public const string QuizRetryNotAvailable = Prefix + "quiz.retry-unavailable";
    public const string QuizAnswerUnknown = Prefix + "quiz.answer-unknown";
    public const string CoursePrerequisiteUnmet = Prefix + "course.prerequisite-unmet";

    // ── Referrals and the sign-up bonus ─────────────────────────────────────────────────────

    public const string ReferralSelf = Prefix + "referral.self";
    public const string ReferralDuplicate = Prefix + "referral.duplicate";
    public const string ReferralNotEligible = Prefix + "referral.not-eligible";
    public const string SignupBonusNotEligible = Prefix + "signup-bonus.not-eligible";

    // ── Rewards ─────────────────────────────────────────────────────────────────────────────

    public const string RewardNotAvailable = Prefix + "reward.not-available";
    public const string RewardInsufficientPoints = Prefix + "reward.insufficient-points";
    public const string RewardLimitReached = Prefix + "reward.limit-reached";
    public const string RewardCooldown = Prefix + "reward.cooldown";
    public const string RewardVersionStale = Prefix + "reward.version-stale";
    public const string RewardPointsUnavailable = Prefix + "reward.points-unavailable";

    // ── Directory and leaderboard ───────────────────────────────────────────────────────────

    public const string LeaderboardScopeUnknown = Prefix + "leaderboard.scope-unknown";
    public const string LeaderboardScopeNeedsSubject = Prefix + "leaderboard.scope-needs-subject";

    // ── Idempotency ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The first request carrying this key has not committed yet. Retryable, and the client's
    /// existing policy already treats it so.
    /// </summary>
    public const string IdempotencyKeyInFlight = Prefix + "idempotency.in-flight";

    /// <summary>
    /// The key was already used by this caller for a different target — another reward, milestone,
    /// challenge or grant. Not retryable: the client reused a key it should not have.
    /// </summary>
    public const string IdempotencyKeyReused = Prefix + "idempotency.key-reused";
}
