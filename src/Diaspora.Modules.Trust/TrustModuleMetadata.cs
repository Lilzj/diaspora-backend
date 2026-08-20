namespace Diaspora.Modules.Trust;

/// <summary>Names shared by the module's platform module and every contributor it declares.</summary>
public static class TrustModuleMetadata
{
    public const string Name = "Trust";

    public const string Version = "1.0.0";

    /// <summary>
    /// The module's own database schema. It owns these tables and no others — the shared
    /// <c>DiasporaDbContext</c> verifies that at startup.
    /// </summary>
    public const string Schema = "trust";

    /// <summary>
    /// The route prefix every Trust endpoint hangs off.
    /// <para>
    /// Deliberately <c>/trust</c> and never <c>/trust/decisions</c>: that path is already
    /// owned by Identity's <c>TrustDecisionsController</c>, which is the inbound channel
    /// this module *calls* to report an outcome. Serving it here would collide with the
    /// one contract ADR-002 depends on.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/trust";
}

/// <summary>
/// The permissions the Trust module owns.
/// <para>
/// All of them live here rather than in Identity — the B2 mistake B2.6 exists to correct.
/// Trust is the only thing that knows what a verification review is worth.
/// </para>
/// </summary>
public static class TrustPermissions
{
    /// <summary>Read your own trust profile, verifications, appeals and reputation.</summary>
    public const string ReadSelf = "trust:read-self";

    /// <summary>Submit verifications and attach documents to them.</summary>
    public const string SubmitVerification = "trust:verification:submit";

    /// <summary>File an appeal against a trust decision.</summary>
    public const string SubmitAppeal = "trust:appeal:submit";

    /// <summary>Record a community confirmation on someone else's contribution.</summary>
    public const string Confirm = "trust:contribution:confirm";

    /// <summary>Read another member's public trust projection.</summary>
    public const string ReadPublic = "trust:read-public";

    /// <summary>Review verification submissions. Staff only.</summary>
    public const string ReviewVerification = "trust:verification:review";

    /// <summary>Review appeals. Staff only, and never the original decision-maker.</summary>
    public const string ReviewAppeal = "trust:appeal:review";

    /// <summary>Work the moderation queue.</summary>
    public const string Moderate = "trust:moderate";

    /// <summary>Assign and escalate queue items. Lead moderators and admins.</summary>
    public const string ManageQueue = "trust:queue:manage";

    /// <summary>Award or revoke a badge outside the automatic rules. Admins.</summary>
    public const string ManageBadges = "trust:badge:manage";
}

/// <summary>
/// The feature gates the Trust module owns. These tell the client what to show; the
/// permissions above decide what the server will actually do.
/// </summary>
public static class TrustFeatures
{
    public const string TrustCenter = "trust.center";
    public const string DiasporaVerification = "trust.verification.diaspora";
    public const string IdentityVerification = "trust.verification.identity";
    public const string CountryVerification = "trust.verification.country";
    public const string Appeals = "trust.appeals";
    public const string Reputation = "trust.reputation";
    public const string ModerationQueue = "trust.moderation.queue";
}

/// <summary>The authorisation policies the Trust module's endpoints reference.</summary>
public static class TrustPolicies
{
    public const string ReadSelf = "trust.read-self";
    public const string SubmitVerification = "trust.verification.submit";
    public const string SubmitAppeal = "trust.appeal.submit";
    public const string Confirm = "trust.contribution.confirm";
    public const string ReadPublic = "trust.read-public";
    public const string ReviewVerification = "trust.verification.review";
    public const string ReviewAppeal = "trust.appeal.review";
    public const string Moderate = "trust.moderate";
    public const string ManageQueue = "trust.queue.manage";
    public const string ManageBadges = "trust.badge.manage";
}

/// <summary>The Trust module's telemetry source name.</summary>
public static class TrustTelemetry
{
    public const string SourceName = "Diaspora.Trust";
}

/// <summary>
/// Stable, client-facing error codes for the module's problem documents.
/// <para>
/// Frozen alongside the API: a client branching on <c>trust.verification.duplicate</c>
/// must keep working across releases, so these strings change only with a new API version.
/// </para>
/// </summary>
public static class TrustErrorCodes
{
    public const string NotAuthenticated = "trust.not-authenticated";
    public const string VerificationNotFound = "trust.verification.not-found";
    public const string VerificationDuplicate = "trust.verification.duplicate";
    public const string VerificationNotPending = "trust.verification.not-pending";
    public const string VerificationTypeUnsupported = "trust.verification.type-unsupported";

    /// <summary>B3.0.2. In-process submission only — unreachable from any HTTP route.</summary>
    public const string SubmissionNotPermitted = "trust.submission.not-permitted";

    /// <summary>B3.0.2. In-process submission only.</summary>
    public const string DocumentTypeUnsupported = "trust.document.type-unsupported";
    public const string DocumentNotFound = "trust.document.not-found";
    public const string DocumentExpired = "trust.document.expired";
    public const string AppealNotFound = "trust.appeal.not-found";
    public const string AppealNotAllowed = "trust.appeal.not-allowed";

    /// <summary>NG-42. An appeal review decision that is none of Approve, Reject, Escalate.</summary>
    public const string AppealDecisionUnrecognised = "trust.appeal.decision-unrecognised";
    public const string AppealAlreadyOpen = "trust.appeal.already-open";
    public const string AppealWindowClosed = "trust.appeal.window-closed";
    public const string SelfReviewForbidden = "trust.review.self-forbidden";
    public const string ProfileNotFound = "trust.profile.not-found";
    public const string QueueItemNotFound = "trust.queue.not-found";
    public const string QueueItemNotAssigned = "trust.queue.not-assigned";
    public const string ConfirmationSelfForbidden = "trust.confirmation.self-forbidden";
    public const string ConfirmationDuplicate = "trust.confirmation.duplicate";
}
