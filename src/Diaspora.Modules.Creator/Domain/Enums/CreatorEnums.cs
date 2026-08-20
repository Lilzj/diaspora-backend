namespace Diaspora.Modules.Creator.Domain.Enums;

/// <summary>
/// Where a member sits in the creator programme.
/// <para>
/// <b>Every one of these is decided here and never by a client.</b> The register is explicit:
/// "§7 forbids the UI inventing that rule from a point count. A client deriving status from
/// <c>combinedScore</c> would be shipping the business rule, and would disagree with the backend
/// the day it changed." The rules that move a member between them live in
/// <c>Domain.Policies.CreatorStatusPolicy</c> and are served to the client as
/// <c>nextStatus.requirements</c> in words, so the app can explain progression without holding it.
/// </para>
/// <para>
/// <b>Three of the nine are not rungs on the ladder at all.</b> <see cref="PendingVerification"/>,
/// <see cref="Restricted"/> and <see cref="Suspended"/> describe a state the member is in rather
/// than a level they reached, and they take precedence over whatever rung they earned — a
/// suspended Top Creator is Suspended. That is why the policy resolves them first and why the
/// earned rung is persisted separately, so a lifted suspension restores the rung rather than
/// starting the climb again.
/// </para>
/// </summary>
public enum CreatorStatus
{
    /// <summary>No recognised creator activity. The default, and not a failure.</summary>
    NotACreator = 0,

    /// <summary>Has contributed something the programme recognises.</summary>
    Contributor = 1,

    /// <summary>Contributing consistently. The first rung with standing.</summary>
    EmergingCreator = 2,

    /// <summary>Has passed the creator-programme verification. Not the same as identity-verified.</summary>
    VerifiedCreator = 3,

    /// <summary>Verified, and sustained.</summary>
    EstablishedCreator = 4,

    /// <summary>The top rung.</summary>
    TopCreator = 5,

    /// <summary>A creator-programme application is with a reviewer.</summary>
    PendingVerification = 6,

    /// <summary>Limited by the platform. Can still read; cannot change what the platform prohibits.</summary>
    Restricted = 7,

    /// <summary>Suspended by the platform.</summary>
    Suspended = 8
}

/// <summary>
/// The creator-programme application lifecycle.
/// <para>
/// <b>Distinct from Trust's <c>VerificationStatus</c>, and the two must never be conflated.</b>
/// Trust answers "is this person who they say they are"; this answers "does this member qualify
/// for the creator programme". A member can be identity-verified and not a verified creator; the
/// reverse is refused here, because identity verification is a requirement of this application
/// rather than a consequence of it.
/// </para>
/// </summary>
public enum CreatorVerificationState
{
    /// <summary>The programme's entry requirements are not met yet.</summary>
    NotEligible = 0,

    /// <summary>May start an application. Nothing has been started.</summary>
    Eligible = 1,

    /// <summary>Started, editable, not with a reviewer.</summary>
    Draft = 2,

    /// <summary>Handed in. Waiting to be picked up.</summary>
    Submitted = 3,

    /// <summary>With a reviewer.</summary>
    UnderReview = 4,

    /// <summary>The reviewer asked for something. Returns to Draft on the member's next edit.</summary>
    MoreInformationRequired = 5,

    /// <summary>Approved. The member is a verified creator until <c>ExpiresAt</c>.</summary>
    Approved = 6,

    /// <summary>Refused. May be appealed once.</summary>
    Rejected = 7,

    /// <summary>An approval lapsed.</summary>
    Expired = 8,

    /// <summary>Withdrawn by the platform from any state.</summary>
    Suspended = 9,

    /// <summary>A refusal is being appealed.</summary>
    AppealPending = 10
}

/// <summary>What a piece of supporting material is worth to a reviewer.</summary>
public enum CreatorEvidenceStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2
}

/// <summary>
/// A milestone's state for one creator.
/// <para>
/// <b><see cref="Completed"/> and <see cref="Claimed"/> are different states and the difference is
/// the whole point.</b> The register: "Nothing stores 'this member completed First 10 Guides and
/// has not claimed it'." A platform that only counted would have no way to answer that, and would
/// pay a reward twice for one milestone.
/// </para>
/// </summary>
public enum CreatorMilestoneState
{
    Locked = 0,
    InProgress = 1,

    /// <summary>Past the programme's "nearly there" mark. Presentation, decided here.</summary>
    AlmostComplete = 2,

    Completed = 3,
    Claimed = 4,
    Expired = 5
}

/// <summary>A challenge's state, from the point of view of one creator.</summary>
public enum CreatorChallengeState
{
    Upcoming = 0,
    Open = 1,
    Joined = 2,
    InProgress = 3,
    Completed = 4,
    Claimed = 5,
    Failed = 6,
    Expired = 7,
    Cancelled = 8
}

/// <summary>A grant programme's own state. Not an application's.</summary>
public enum CommunityGrantState
{
    Upcoming = 0,
    Open = 1,

    /// <summary>Open, and inside the programme's closing window. Decided here, never on a device.</summary>
    ClosingSoon = 2,

    Closed = 3
}

/// <summary>
/// A grant application's lifecycle.
/// <para>
/// Nine states, matching the client's <c>GRANT_APPLICATION_STATES</c> exactly — including
/// <see cref="MoreInformationRequested"/>, which is spelled "Requested" here and "Required" on the
/// creator verification. The two are different words in the client's own vocabulary and matching
/// each is what stops a label falling through.
/// </para>
/// </summary>
public enum GrantApplicationState
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    MoreInformationRequested = 3,
    Approved = 4,
    Rejected = 5,
    Withdrawn = 6,

    /// <summary>Approved, funded, and delivering. Reporting is due from here.</summary>
    Active = 7,

    Completed = 8
}

/// <summary>A required report on an active grant.</summary>
public enum GrantReportState
{
    Pending = 0,
    Submitted = 1,
    Accepted = 2,
    Overdue = 3
}

/// <summary>What kind of answer a grant form question takes.</summary>
public enum GrantFieldKind
{
    Text = 0,
    LongText = 1,
    Number = 2,
    Select = 3
}

/// <summary>How difficult a course is.</summary>
public enum CourseLevel
{
    Beginner = 0,
    Intermediate = 1,
    Advanced = 2
}

/// <summary>What a lesson is.</summary>
public enum LessonKind
{
    Reading = 0,
    Video = 1,
    Checklist = 2,

    /// <summary>Finished by passing its quiz, never by asserting completion.</summary>
    Quiz = 3
}

/// <summary>
/// One piece of lesson content.
/// <para>
/// <b>A closed set rather than markup, and that is a security decision rather than a modelling
/// preference.</b> The register: "A content service that could send markup could send a script.
/// The renderer knows how to draw seven block kinds and nothing else." A block whose kind is not
/// one of these is never stored and never served.
/// </para>
/// </summary>
public enum LessonBlockKind
{
    Heading = 0,
    Paragraph = 1,
    List = 2,
    Callout = 3,
    Checklist = 4,
    Quote = 5,
    Action = 6
}

/// <summary>The tone of a callout block.</summary>
public enum CalloutTone
{
    Info = 0,
    Success = 1,
    Warning = 2
}

/// <summary>One creator's progress through one course.</summary>
public enum CourseProgressState
{
    NotStarted = 0,
    InProgress = 1,
    Completed = 2
}

/// <summary>
/// Where one referral stands.
/// <para>
/// <b>Four of these are the register's central point.</b> "§18: Do not assume every signup is a
/// qualifying referral." <see cref="Invited"/>, <see cref="SignedUp"/>, <see cref="Qualified"/> and
/// <see cref="Rewarded"/> are four different facts, and collapsing any two of them is how a
/// dashboard comes to promise money the platform never owed.
/// </para>
/// </summary>
public enum ReferralState
{
    /// <summary>The code was shared. Nobody has used it.</summary>
    Invited = 0,

    /// <summary>Somebody registered with the code. That is all it means.</summary>
    SignedUp = 1,

    /// <summary>Registered, and working through the qualification rule.</summary>
    Pending = 2,

    /// <summary>The qualification rule is satisfied. A reward is owed.</summary>
    Qualified = 3,

    /// <summary>The reward has been granted.</summary>
    Rewarded = 4,

    /// <summary>The window closed before qualification.</summary>
    Expired = 5,

    /// <summary>Refused. The reason the member sees never names the check that refused it.</summary>
    Rejected = 6
}

/// <summary>
/// The sign-up bonus.
/// <para>
/// <see cref="Blocked"/> is a risk refusal and carries a member-safe reason and nothing else — see
/// <c>CreatorErrors.SignupBonusNotEligible</c>.
/// </para>
/// </summary>
public enum SignupBonusState
{
    NotEligible = 0,
    Eligible = 1,

    /// <summary>Acknowledged, and waiting on an outstanding requirement.</summary>
    Pending = 2,

    Awarded = 3,
    AlreadyClaimed = 4,
    Expired = 5,
    Blocked = 6
}

/// <summary>A reward's availability, and a redemption's outcome. One vocabulary for both.</summary>
public enum CreatorRewardState
{
    Available = 0,
    Locked = 1,
    Pending = 2,
    Redeemed = 3,
    Rejected = 4,
    Expired = 5
}

/// <summary>
/// How the programme values points a member holds.
/// <para>
/// Served on the rules catalogue so the answer to "do my points expire" is the platform's rather
/// than a guess. <see cref="Never"/> is a real answer and the platform's current one.
/// </para>
/// </summary>
public enum PointsExpiryPolicy
{
    /// <summary>Points do not lapse. What the platform does today.</summary>
    Never = 0,

    /// <summary>Points lapse a fixed period after they are earned.</summary>
    AfterPeriod = 1,

    /// <summary>Points lapse a fixed period after the member's last recognised action.</summary>
    AfterInactivity = 2
}

/// <summary>
/// Which ranking a leaderboard request means.
/// <para>
/// Five, matching the client's <c>LEADERBOARD_SCOPES</c>, and no sixth. §15 of the phase brief:
/// "Do not invent additional ranking dimensions unless required by the source contract."
/// </para>
/// </summary>
public enum LeaderboardScope
{
    TopCreators = 0,
    TopContributors = 1,
    Challenge = 2,
    Community = 3,
    Country = 4
}

/// <summary>
/// The windows a leaderboard can be asked for.
/// <para>
/// <b>Only the ones the recognised-activity ledger can genuinely answer.</b> Every entry carries an
/// <c>OccurredAt</c>, so a sum over a window is a real figure rather than a lifetime total with a
/// period label on it — which is the failure CREATOR-GAP-007 records for analytics and which this
/// module does not repeat for ranking.
/// </para>
/// </summary>
public enum LeaderboardPeriod
{
    AllTime = 0,
    Last7Days = 1,
    Last30Days = 2,
    Last90Days = 3,
    Year = 4
}

/// <summary>
/// How far a challenge task's rule reaches back for the activity it counts.
/// <para>
/// A challenge that counted a guide published two years before it opened would pay for work
/// somebody had already been paid for, which is why <see cref="SinceJoined"/> is the default.
/// </para>
/// </summary>
public enum ChallengeTaskWindow
{
    /// <summary>Counts activity recorded after the member joined. The default and the safe one.</summary>
    SinceJoined = 0,

    /// <summary>Counts activity inside the challenge's own dates.</summary>
    ChallengeWindow = 1,

    /// <summary>Counts everything the creator has ever done. For a "reach 50 guides" shape.</summary>
    Lifetime = 2
}
