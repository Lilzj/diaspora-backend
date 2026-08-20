namespace Diaspora.Modules.Jobs;

/// <summary>
/// The authorisation policies the module's endpoints reference.
/// <para>
/// Dot-separated names where permissions use colons, per the platform convention. Each policy
/// names exactly one permission from <see cref="JobPermissions"/>; the mapping is declared in
/// <c>Authorization/JobPolicyContributor</c> and registered with ASP.NET Core by the kernel.
/// Endpoints always reference a policy name, never a raw permission.
/// </para>
/// </summary>
public static class JobPolicies
{
    // Employer
    public const string EmployerCreate = "jobs.employer.create";
    public const string EmployerManage = "jobs.employer.manage";
    public const string EmployerManageAny = "jobs.employer.manage.any";
    public const string EmployerSubmitVerification = "jobs.employer.verify.submit";

    // Posting authoring & lifecycle
    public const string PostCreate = "jobs.post.create";
    public const string PostEditOwn = "jobs.post.edit.own";
    public const string PostEditAny = "jobs.post.edit.any";
    public const string PostDeleteOwn = "jobs.post.delete.own";
    public const string PostPublish = "jobs.post.publish";
    public const string PostClose = "jobs.post.close";
    public const string PostArchive = "jobs.post.archive";
    public const string PostRestore = "jobs.post.restore";
    public const string PostFeature = "jobs.post.feature";
    public const string PostSubmitVerification = "jobs.post.verify.submit";

    // Hiring
    public const string PipelineManage = "jobs.pipeline.manage";
    public const string ApplicationsReview = "jobs.applications.review";
    public const string ApplicationsDecide = "jobs.applications.decide";
    public const string InterviewsSchedule = "jobs.interviews.schedule";
    public const string InterviewsRecord = "jobs.interviews.record";
    public const string OffersExtend = "jobs.offers.extend";
    public const string OffersWithdraw = "jobs.offers.withdraw";

    // Candidate
    public const string CandidateManage = "jobs.candidate.manage";
    public const string ResumeManage = "jobs.resume.manage";
    public const string Apply = "jobs.apply.submit";
    public const string ApplicationWithdraw = "jobs.application.withdraw";
    public const string OffersRespond = "jobs.offers.respond";
    public const string SavedManage = "jobs.saved.manage";
    public const string AlertsManage = "jobs.alerts.manage";

    // Shared
    public const string Report = "jobs.report";
    public const string ViewAnalytics = "jobs.analytics.view";

    // Staff
    public const string Moderate = "jobs.moderate";
    public const string ManageCategories = "jobs.categories.manage";
    public const string ManageSkills = "jobs.skills.manage";
}
