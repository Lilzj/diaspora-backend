using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Jobs.Authorization;

/// <summary>
/// Declares the policies the module's endpoints reference.
/// <para>
/// Every policy names exactly one permission from <see cref="JobPermissions"/>. The kernel
/// validates at startup that each named permission exists, so a typo fails the boot rather than
/// silently denying a route forever.
/// </para>
/// <para>
/// <b>A policy is a necessary condition, never a sufficient one, for anything employer-scoped.</b>
/// <c>jobs.post.publish</c> says the caller may publish jobs somewhere; whether they may publish
/// <em>this</em> job is the employer aggregate's answer, and every such handler asks it. Reading
/// the policy list as the authorisation model would understate it by half.
/// </para>
/// </summary>
public sealed class JobPolicyContributor : IPolicyContributor
{
    public string Module => JobsModuleMetadata.Name;

    public IEnumerable<PolicyDefinition> GetPolicies()
    {
        // Employer
        yield return Policy(JobPolicies.EmployerCreate, JobPermissions.EmployerCreate);
        yield return Policy(JobPolicies.EmployerManage, JobPermissions.EmployerManage);
        yield return Policy(JobPolicies.EmployerManageAny, JobPermissions.EmployerManageAny);
        yield return Policy(
            JobPolicies.EmployerSubmitVerification, JobPermissions.EmployerSubmitVerification);

        // Posting authoring & lifecycle
        yield return Policy(JobPolicies.PostCreate, JobPermissions.PostCreate);
        yield return Policy(JobPolicies.PostEditOwn, JobPermissions.PostEditOwn);
        yield return Policy(JobPolicies.PostEditAny, JobPermissions.PostEditAny);
        yield return Policy(JobPolicies.PostDeleteOwn, JobPermissions.PostDeleteOwn);
        yield return Policy(JobPolicies.PostPublish, JobPermissions.PostPublish);
        yield return Policy(JobPolicies.PostClose, JobPermissions.PostClose);
        yield return Policy(JobPolicies.PostArchive, JobPermissions.PostArchive);
        yield return Policy(JobPolicies.PostRestore, JobPermissions.PostRestore);
        yield return Policy(JobPolicies.PostFeature, JobPermissions.PostFeature);
        yield return Policy(
            JobPolicies.PostSubmitVerification, JobPermissions.PostSubmitVerification);

        // Hiring
        yield return Policy(JobPolicies.PipelineManage, JobPermissions.PipelineManage);
        yield return Policy(JobPolicies.ApplicationsReview, JobPermissions.ApplicationsReview);
        yield return Policy(JobPolicies.ApplicationsDecide, JobPermissions.ApplicationsDecide);
        yield return Policy(JobPolicies.InterviewsSchedule, JobPermissions.InterviewsSchedule);
        yield return Policy(JobPolicies.InterviewsRecord, JobPermissions.InterviewsRecord);
        yield return Policy(JobPolicies.OffersExtend, JobPermissions.OffersExtend);
        yield return Policy(JobPolicies.OffersWithdraw, JobPermissions.OffersWithdraw);

        // Candidate
        yield return Policy(JobPolicies.CandidateManage, JobPermissions.CandidateManage);
        yield return Policy(JobPolicies.ResumeManage, JobPermissions.ResumeManage);
        yield return Policy(JobPolicies.Apply, JobPermissions.Apply);
        yield return Policy(JobPolicies.ApplicationWithdraw, JobPermissions.ApplicationWithdraw);
        yield return Policy(JobPolicies.OffersRespond, JobPermissions.OffersRespond);
        yield return Policy(JobPolicies.SavedManage, JobPermissions.SavedManage);
        yield return Policy(JobPolicies.AlertsManage, JobPermissions.AlertsManage);

        // Shared
        yield return Policy(JobPolicies.Report, JobPermissions.Report);
        yield return Policy(JobPolicies.ViewAnalytics, JobPermissions.ViewAnalytics);

        // Staff
        yield return Policy(JobPolicies.Moderate, JobPermissions.Moderate);
        yield return Policy(JobPolicies.ManageCategories, JobPermissions.ManageCategories);
        yield return Policy(JobPolicies.ManageSkills, JobPermissions.ManageSkills);
    }

    private static PolicyDefinition Policy(string name, string permission) =>
        PolicyDefinition.ForPermission(name, JobsModuleMetadata.Name, permission);
}
