namespace Diaspora.Modules.Jobs;

/// <summary>
/// The permissions the Jobs module owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use
/// dots — see <see cref="JobPolicies"/>). Tier floors live with the definitions in
/// <c>Authorization/JobPermissionContributor</c>, because the module is the only thing that
/// knows what each capability is worth.
/// </para>
/// <para>
/// The split that matters: <b>looking is Limited, being seen is Verified.</b> Browsing
/// postings, saving them, setting alerts and drafting a candidate profile are private,
/// low-stakes acts that cost nobody anything. Applying, publishing a posting, registering an
/// employer and every recruiting act put a real identity in front of a stranger who will make
/// a decision about someone's livelihood — which is exactly the progression B2.0.1 gates on
/// proven identity.
/// </para>
/// <para>
/// Staff-only capabilities are role-scoped, not tier-scoped, per the access-tier semantics: a
/// tier measures progression, a role grants privilege.
/// </para>
/// <para>
/// <see cref="Apply"/> keeps the exact key the host declared provisionally from B2 onwards
/// (<c>jobs:apply</c>) at its original Verified floor. It is a wire contract the client
/// already holds.
/// </para>
/// </summary>
public static class JobPermissions
{
    // ── Employer ────────────────────────────────────────────────────────────────────────────

    public const string EmployerCreate = "jobs:employer:create";
    public const string EmployerManage = "jobs:employer:manage";
    public const string EmployerManageAny = "jobs:employer:manage:any";
    public const string EmployerSubmitVerification = "jobs:employer:verify:submit";

    // ── Posting authoring & lifecycle ───────────────────────────────────────────────────────

    public const string PostCreate = "jobs:post:create";
    public const string PostEditOwn = "jobs:post:edit:own";
    public const string PostEditAny = "jobs:post:edit:any";
    public const string PostDeleteOwn = "jobs:post:delete:own";
    public const string PostPublish = "jobs:post:publish";
    public const string PostClose = "jobs:post:close";
    public const string PostArchive = "jobs:post:archive";
    public const string PostRestore = "jobs:post:restore";
    public const string PostFeature = "jobs:post:feature";
    public const string PostSubmitVerification = "jobs:post:verify:submit";

    // ── Hiring ──────────────────────────────────────────────────────────────────────────────

    public const string PipelineManage = "jobs:pipeline:manage";
    public const string ApplicationsReview = "jobs:applications:review";
    public const string ApplicationsDecide = "jobs:applications:decide";
    public const string InterviewsSchedule = "jobs:interviews:schedule";
    public const string InterviewsRecord = "jobs:interviews:record";
    public const string OffersExtend = "jobs:offers:extend";
    public const string OffersWithdraw = "jobs:offers:withdraw";

    // ── Candidate ───────────────────────────────────────────────────────────────────────────

    public const string CandidateManage = "jobs:candidate:manage";
    public const string ResumeManage = "jobs:resume:manage";

    /// <summary>
    /// Apply to a job listing. Declared provisionally by the host from B2; claimed here.
    /// </summary>
    public const string Apply = "jobs:apply";

    public const string ApplicationWithdraw = "jobs:application:withdraw";
    public const string OffersRespond = "jobs:offers:respond";
    public const string SavedManage = "jobs:saved:manage";
    public const string AlertsManage = "jobs:alerts:manage";

    // ── Shared ──────────────────────────────────────────────────────────────────────────────

    public const string Report = "jobs:report";
    public const string ViewAnalytics = "jobs:analytics:view";

    // ── Staff ───────────────────────────────────────────────────────────────────────────────

    public const string Moderate = "jobs:moderate";
    public const string ManageCategories = "jobs:categories:manage";
    public const string ManageSkills = "jobs:skills:manage";
}
