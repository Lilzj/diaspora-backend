namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// The kinds of verification the platform supports.
/// <para>
/// Names match the frontend's <c>VerificationType</c> union exactly — the client uses these
/// as object keys in <c>Record&lt;VerificationType, VerificationStatus&gt;</c>, so a rename
/// here silently breaks a render. <c>Diaspora</c> and <c>Residence</c> are additions the
/// frontend needs but never named: the diaspora wizard posts to its own route and the
/// residency flow was folded into <c>Country</c>.
/// </para>
/// <para>
/// This enum is the one place a new verification type must be named. Everything else about
/// it — evidence requirements, review routing, approval effects — is supplied by an
/// <c>IVerificationTypeHandler</c> the module discovers, so adding a type is an addition
/// rather than an edit.
/// </para>
/// </summary>
public enum VerificationType
{
    /// <summary>Phone possession. Owned by Identity; mirrored here for the checklist.</summary>
    Phone = 0,

    /// <summary>Email possession. Owned by Identity; mirrored here for the checklist.</summary>
    Email = 1,

    /// <summary>Government ID plus biometric selfie match.</summary>
    Identity = 2,

    /// <summary>Residency in a specific country community.</summary>
    Country = 3,

    /// <summary>Professional credential or employment.</summary>
    Professional = 4,

    /// <summary>Business registration.</summary>
    Business = 5,

    /// <summary>A community organisation's legitimacy.</summary>
    Organization = 6,

    /// <summary>Embassy or consulate endorsement.</summary>
    Embassy = 7,

    /// <summary>The core eligibility check: nationality differs from country of residence.</summary>
    Diaspora = 8,

    /// <summary>Proof of address, distinct from country-level residency.</summary>
    Residence = 9,

    /// <summary>Employment letter or contract.</summary>
    Employment = 10,

    /// <summary>Community guide eligibility.</summary>
    CommunityGuide = 11,

    /// <summary>Contributor eligibility — the right to publish guides.</summary>
    Contributor = 12
}

/// <summary>
/// Where a verification request currently sits.
/// <para>
/// The frontend declares this enum twice with different members —
/// <c>types/trust.ts</c> includes <c>Expired</c>, <c>types/verification.ts</c> does not.
/// This is the union of both plus the workflow states the client's
/// <c>DiasporaVerificationStatus</c> already renders, so one server enum feeds every screen.
/// </para>
/// </summary>
public enum VerificationStatus
{
    /// <summary>Created but not yet handed to review. Draft-equivalent.</summary>
    Submitted = 0,

    /// <summary>Awaiting a reviewer. The frontend renders this as "Pending".</summary>
    Pending = 1,

    /// <summary>A reviewer has it open.</summary>
    UnderReview = 2,

    /// <summary>Reviewer asked for more evidence. Frontend: "NeedMoreInfo".</summary>
    NeedMoreInfo = 3,

    /// <summary>Escalated beyond routine review.</summary>
    UnderInvestigation = 4,

    /// <summary>Granted.</summary>
    Approved = 5,

    /// <summary>Some claims granted, others not.</summary>
    PartiallyApproved = 6,

    /// <summary>Refused.</summary>
    Rejected = 7,

    /// <summary>Was approved; the evidence has since lapsed.</summary>
    Expired = 8,

    /// <summary>Was approved; withdrawn for cause.</summary>
    Revoked = 9,

    /// <summary>Refused, and the member has appealed.</summary>
    Appealed = 10,

    /// <summary>Approved, but fresh evidence is now required.</summary>
    ReverificationRequired = 11
}

/// <summary>How a verification reached its outcome.</summary>
public enum VerificationDecisionMode
{
    /// <summary>A human reviewer decided.</summary>
    ManualReview = 0,

    /// <summary>A rule approved it without a human.</summary>
    AutomaticApproval = 1,

    /// <summary>A rule refused it without a human.</summary>
    AutomaticRejection = 2,

    /// <summary>A rule routed it to a human.</summary>
    RoutedToReview = 3
}

/// <summary>
/// Document types accepted as evidence.
/// <para>
/// The union of the frontend's <c>DiasporaDocumentType</c> and
/// <c>VerificationDocumentType</c>, which overlap but disagree — the diaspora wizard offers
/// <c>Passport</c>, the country screen offers <c>PassportStamp</c>. Both are kept so neither
/// screen has to change.
/// </para>
/// </summary>
public enum VerificationDocumentType
{
    Passport = 0,
    PassportStamp = 1,
    ResidencePermit = 2,
    WorkPermit = 3,
    OccupationPermit = 4,
    StudentPermit = 5,
    StudentVisa = 6,
    Visa = 7,
    GovernmentImmigrationDocuments = 8,
    NationalId = 9,
    DriverLicense = 10,
    UtilityBill = 11,
    BankStatement = 12,
    ProofOfAddress = 13,
    EmploymentLetter = 14,
    BusinessRegistration = 15,
    OrganizationDocument = 16,
    GuideEvidence = 17,
    Other = 18
}

/// <summary>The lifecycle of one attached document.</summary>
public enum VerificationDocumentStatus
{
    /// <summary>Attached, not yet looked at.</summary>
    Pending = 0,

    /// <summary>A reviewer accepted this document.</summary>
    Accepted = 1,

    /// <summary>A reviewer rejected this document.</summary>
    Rejected = 2,

    /// <summary>Replaced by a newer version of the same document.</summary>
    Superseded = 3,

    /// <summary>Past its expiry date.</summary>
    Expired = 4
}

/// <summary>
/// Machine-readable refusal reasons.
/// <para>
/// Serialised in SCREAMING_SNAKE to match the frontend's
/// <c>VerificationResponse.rejectionReasonCode</c> union, which the client compares as
/// string literals. The first four are the frontend's exact set; the rest extend it.
/// </para>
/// </summary>
public enum VerificationRejectionReason
{
    INVALID_DOCUMENT = 0,
    EXPIRED_DOCUMENT = 1,
    MISMATCHED_NATIONALITY = 2,
    ADDITIONAL_DOCS_NEEDED = 3,
    ILLEGIBLE_DOCUMENT = 4,
    FACE_MATCH_FAILED = 5,
    RESIDENCE_NOT_ESTABLISHED = 6,
    NOT_DIASPORA_ELIGIBLE = 7,
    SUSPECTED_FRAUD = 8,
    DUPLICATE_SUBMISSION = 9
}

/// <summary>
/// A member's overall account health.
/// <para>
/// The frontend spells the last member <c>'Under Review'</c> — with a space, which no C#
/// enum and no sane serializer contract can carry. Emitted as <c>UnderReview</c>; the
/// client already renders these through a label map, so the cost is one map entry.
/// </para>
/// </summary>
public enum AccountHealth
{
    Excellent = 0,
    Good = 1,
    Restricted = 2,
    Suspended = 3,
    UnderReview = 4
}

/// <summary>Coarse risk banding. Matches the frontend's <c>RiskLevel</c>.</summary>
public enum RiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>
/// Signals that push a member's risk score up.
/// <para>
/// Serialised in the frontend's exact <c>Screaming_Snake</c> casing — these render directly
/// as chips in the admin risk panel.
/// </para>
/// </summary>
public enum RiskFactor
{
    VPN_Detected = 0,
    Device_Change = 1,
    Rapid_Posting = 2,
    Duplicate_Content = 3,
    Failed_Verification = 4,
    Reports_Spike = 5
}

/// <summary>
/// The member-facing reputation ladder.
/// <para>
/// The frontend contradicts itself here: <c>ReputationLevel</c> ends in
/// <c>CommunityAmbassador</c>, while <c>CountryReputationScore.level</c> ends in
/// <c>Ambassador</c>, for the same ladder. The longer name wins — it is the one with
/// thresholds, labels and icons defined against it.
/// </para>
/// </summary>
public enum ReputationLevel
{
    Member = 0,
    VerifiedResident = 1,
    TrustedContributor = 2,
    CommunityExpert = 3,
    CommunityAmbassador = 4
}

/// <summary>
/// The three axes reputation is scored on.
/// <para>
/// Serialised lowercase to match the frontend's <c>ReputationDimension</c>, which keys the
/// <c>DIMENSION_LABELS</c> map the reputation screen renders from.
/// </para>
/// </summary>
public enum ReputationDimension
{
    contribution = 0,
    verification = 1,
    trust = 2
}

/// <summary>
/// Badge kinds.
/// <para>
/// The first seven are the frontend's <c>ReputationBadge.type</c> union exactly. The rest
/// are the badges the brief names which the client does not yet render. New badges are
/// added here <em>and</em> as an <c>IBadgeRule</c>; the rule decides who earns it.
/// </para>
/// </summary>
public enum BadgeType
{
    CountryVerified = 0,
    PhoneVerified = 1,
    EmailVerified = 2,
    GovernmentIdVerified = 3,
    ProfilePhoto = 4,
    ProviderVerified = 5,
    TopContributor = 6,
    VerifiedMember = 7,
    TrustedContributor = 8,
    CommunityGuide = 9,
    VerifiedBusiness = 10,
    VerifiedOrganization = 11,
    EarlyMember = 12,
    Moderator = 13
}

/// <summary>What a member is appealing. Matches the frontend's <c>AppealRequest.type</c>.</summary>
public enum AppealType
{
    Verification = 0,
    Content = 1,
    Suspension = 2,
    Publishing = 3,

    /// <summary>
    /// A hold on the member's reward points (RWD-14, CS-4a). The target is the member themselves;
    /// an approval lifts the hold in the Financial Platform, in the reviewer's name.
    /// </summary>
    RewardHold = 4
}

/// <summary>
/// Where an appeal sits.
/// <para>
/// The frontend's <c>AppealRequest.status</c> is only <c>Pending | Approved | Rejected</c>,
/// but its appeals screen draws a six-stage tracker over
/// <c>PendingReview → Approved → Rejected → Escalated → Appealed → Resolved</c>. This enum
/// carries the states the tracker needs; the three the client compares against are spelled
/// identically.
/// </para>
/// </summary>
public enum AppealStatus
{
    Pending = 0,
    UnderReview = 1,
    Escalated = 2,
    Approved = 3,
    Rejected = 4,
    Withdrawn = 5
}

/// <summary>What kind of thing is in the moderation queue. Matches the frontend.</summary>
public enum ModerationContentType
{
    Guide = 0,
    CommunityUpdate = 1,
    Comment = 2,
    Listing = 3,
    Profile = 4,
    Organization = 5
}

/// <summary>Where a queue item sits. Matches the frontend's <c>ModerationQueueStatus</c>.</summary>
public enum ModerationQueueStatus
{
    AIReview = 0,
    PendingReview = 1,
    Approved = 2,
    Rejected = 3,
    Escalated = 4,
    Appealed = 5,
    Resolved = 6
}

/// <summary>What a moderator did. Matches the frontend's <c>ModerationActionType</c>.</summary>
public enum ModerationActionType
{
    Approve = 0,
    Reject = 1,
    Hide = 2,
    Archive = 3,
    Pin = 4,
    Unpin = 5,
    Suspend = 6,
    Restore = 7,
    Escalate = 8
}

/// <summary>Queue priority, which drives SLA and ordering.</summary>
public enum ModerationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// The module's own trust ladder — what a member has earned, as Trust sees it.
/// <para>
/// Distinct from three things it is easy to confuse it with, and the distinction matters:
/// </para>
/// <list type="bullet">
/// <item><c>Platform.TrustLevel</c> (None/Basic/Verified/Premium) is the coarse <em>read</em>
/// contract other modules consume through <c>ITrustService</c>. This enum projects onto it.
/// A module asking "is this member verified?" gets that four-value answer and is spared
/// every rung Trust invents later.</item>
/// <item><c>Contracts.AccessTier</c> is the authorisation ladder, owned by Identity and
/// derived from access state. Trust never sets it directly — it reports a decision and
/// Identity moves the member.</item>
/// <item><see cref="ReputationLevel"/> is the member-facing progression, driven by points.
/// A member can be a <c>CommunityExpert</c> by reputation while sitting at
/// <see cref="Provisional"/> here because their residency evidence lapsed.</item>
/// </list>
/// </summary>
public enum TrustTier
{
    /// <summary>Nothing established. New or fully revoked.</summary>
    Untrusted = 0,

    /// <summary>Phone or email established. Can read and participate lightly.</summary>
    Basic = 1,

    /// <summary>Held a grant that has lapsed or is under re-verification.</summary>
    Provisional = 2,

    /// <summary>Diaspora-verified. The contributor tier.</summary>
    Verified = 3,

    /// <summary>Verified plus a sustained contribution record.</summary>
    Trusted = 4,

    /// <summary>Trusted plus a moderation or guide mandate.</summary>
    Steward = 5
}

/// <summary>
/// The outcome vocabulary Identity accepts on <c>POST /api/v1/trust/decisions</c>.
/// <para>
/// Fixed by <c>ApplyTrustDecisionRequest.Outcome</c> in the published Identity contract —
/// "One of UnderReview, Granted, Rejected, Revoked". This enum exists so the module cannot
/// send a fifth value that Identity would refuse at runtime.
/// </para>
/// </summary>
public enum TrustDecisionOutcome
{
    UnderReview = 0,
    Granted = 1,
    Rejected = 2,
    Revoked = 3,

    /// <summary>Submitted and waiting for a reviewer. See Identity's copy for why it exists.</summary>
    Submitted = 4
}

/// <summary>
/// Why an eligibility check refused.
/// <para>
/// These are the exact literals the frontend's <c>TrustEngine.evaluateAction</c> returns and
/// its screens branch on. Moving the decision server-side only works if the vocabulary
/// survives the move.
/// </para>
/// </summary>
public enum EligibilityCode
{
    ALLOW = 0,
    DENIED = 1,
    RATE_LIMITED = 2,
    COOLDOWN = 3,
    HEALTH_RESTRICTED = 4
}
