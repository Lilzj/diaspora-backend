using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Jobs.Authorization;

/// <summary>
/// Declares the permissions the Jobs module owns.
/// <para>
/// <b>One product judgement runs through the whole band: looking is Limited, being seen is
/// Verified.</b> Browsing, saving, setting alerts and drafting a candidate profile are private,
/// low-stakes acts that cost nobody anything and are exactly what a new member should be doing.
/// Applying, publishing a posting, registering an employer and every recruiting act put a real
/// identity in front of a stranger who will make a decision about someone's livelihood — which
/// is precisely the progression B2.0.1 gates on proven identity.
/// </para>
/// <para>
/// <b>Employer-scoped authority is not here at all.</b> These permissions answer "may this
/// person recruit"; whether they may recruit <em>for this employer</em> is a membership question
/// only the employer aggregate can answer, and no permission catalogue can express it. Both
/// checks run: the policy gates the endpoint, and the handler asks the aggregate.
/// </para>
/// <para>
/// Staff capabilities — moderating, editing any posting, administering the catalogues — are
/// <b>role-scoped, not tier-scoped</b>, per B2.0.1: a tier measures progression, a role grants
/// privilege. Nothing here sits below <see cref="AccessTier.Limited"/>; a gate below it is
/// granted to phone-unverified accounts.
/// </para>
/// <para>
/// The kernel constructs this type before the container exists, so it has a public parameterless
/// constructor and no dependencies.
/// </para>
/// </summary>
public sealed class JobPermissionContributor : IPermissionContributor
{
    private static readonly HashSet<string> StaffRoles =
        new(StringComparer.Ordinal) { "Moderator", "Admin", "SuperAdmin" };

    private static readonly HashSet<string> AdminRoles =
        new(StringComparer.Ordinal) { "Admin", "SuperAdmin" };

    public string Module => JobsModuleMetadata.Name;

    public IEnumerable<PermissionDefinition> GetPermissions()
    {
        foreach (var permission in EmployerPermissions())
        {
            yield return permission;
        }

        foreach (var permission in PostingPermissions())
        {
            yield return permission;
        }

        foreach (var permission in HiringPermissions())
        {
            yield return permission;
        }

        foreach (var permission in CandidatePermissions())
        {
            yield return permission;
        }

        foreach (var permission in SharedAndStaffPermissions())
        {
            yield return permission;
        }
    }

    private static IEnumerable<PermissionDefinition> EmployerPermissions()
    {
        yield return new PermissionDefinition(
            JobPermissions.EmployerCreate, JobsModuleMetadata.Name,
            "Register an employer profile.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.EmployerManage, JobsModuleMetadata.Name,
            "Manage an employer profile you belong to.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.EmployerSubmitVerification, JobsModuleMetadata.Name,
            "Submit an employer for verification.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> PostingPermissions()
    {
        yield return new PermissionDefinition(
            JobPermissions.PostCreate, JobsModuleMetadata.Name, "Draft a job posting.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.PostEditOwn, JobsModuleMetadata.Name,
            "Edit a job posting belonging to your employer.")
        {
            MinimumTier = AccessTier.Verified,

            // Correcting a live advert is never the thing to withhold: a wrong salary or a
            // wrong closing date wastes candidates' time, not the restricted recruiter's.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.PostDeleteOwn, JobsModuleMetadata.Name,
            "Delete a job posting belonging to your employer.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.PostPublish, JobsModuleMetadata.Name, "Publish a job posting.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.PostClose, JobsModuleMetadata.Name,
            "Close a job posting to applications.")
        {
            MinimumTier = AccessTier.Verified,

            // Taking down a vacancy that no longer exists must never be blocked — leaving it up
            // is what wastes applicants' afternoons.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.PostArchive, JobsModuleMetadata.Name, "Archive a job posting.")
        {
            MinimumTier = AccessTier.Verified,
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.PostRestore, JobsModuleMetadata.Name,
            "Restore an archived or deleted job posting.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.PostFeature, JobsModuleMetadata.Name,
            "Feature or unfeature any job posting.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            JobPermissions.PostSubmitVerification, JobsModuleMetadata.Name,
            "Submit a job posting for verification.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> HiringPermissions()
    {
        yield return new PermissionDefinition(
            JobPermissions.PipelineManage, JobsModuleMetadata.Name,
            "Configure a job's hiring pipeline.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.ApplicationsReview, JobsModuleMetadata.Name,
            "Read applications sent to your employer.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.ApplicationsDecide, JobsModuleMetadata.Name,
            "Shortlist or reject applicants.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.InterviewsSchedule, JobsModuleMetadata.Name, "Schedule interviews.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.InterviewsRecord, JobsModuleMetadata.Name,
            "Record interview outcomes.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.OffersExtend, JobsModuleMetadata.Name,
            "Draft and extend offers to candidates.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.OffersWithdraw, JobsModuleMetadata.Name, "Withdraw an offer.")
        {
            MinimumTier = AccessTier.Verified
        };
    }

    private static IEnumerable<PermissionDefinition> CandidatePermissions()
    {
        yield return new PermissionDefinition(
            JobPermissions.CandidateManage, JobsModuleMetadata.Name,
            "Create and edit your own candidate profile.")
        {
            MinimumTier = AccessTier.Limited,

            // Your own profile stays editable under restriction — including making it private,
            // which is the setting a restricted member most needs to be able to reach.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.ResumeManage, JobsModuleMetadata.Name, "Manage your own résumés.")
        {
            MinimumTier = AccessTier.Limited,
            GrantedWhenRestricted = true
        };

        // The key the host declared provisionally from B2. Same name, same floor.
        yield return new PermissionDefinition(
            JobPermissions.Apply, JobsModuleMetadata.Name, "Apply to a job listing.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.ApplicationWithdraw, JobsModuleMetadata.Name,
            "Withdraw your own application.")
        {
            MinimumTier = AccessTier.Limited,

            // Leaving a hiring process must never be blocked. A restriction that trapped
            // someone in a process they wanted out of would be the platform working against
            // the person it restricted.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.OffersRespond, JobsModuleMetadata.Name,
            "Accept or decline an offer made to you.")
        {
            MinimumTier = AccessTier.Limited,

            // An offer is time-limited and life-changing. A restriction must not cost someone
            // a job by running out the clock.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.SavedManage, JobsModuleMetadata.Name, "Save and unsave jobs.")
        {
            MinimumTier = AccessTier.Limited,

            // Saving is private and costs nobody anything.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.AlertsManage, JobsModuleMetadata.Name, "Manage your job alerts.")
        {
            MinimumTier = AccessTier.Limited
        };
    }

    private static IEnumerable<PermissionDefinition> SharedAndStaffPermissions()
    {
        yield return new PermissionDefinition(
            JobPermissions.Report, JobsModuleMetadata.Name,
            "Report a job posting or an employer.")
        {
            MinimumTier = AccessTier.Limited,

            // Reporting a fraudulent job advert must survive a restriction, or the restriction
            // silences the person best placed to flag it. On this platform that advert is the
            // most damaging content the module can carry.
            GrantedWhenRestricted = true
        };

        yield return new PermissionDefinition(
            JobPermissions.ViewAnalytics, JobsModuleMetadata.Name,
            "Read hiring statistics for your employer.")
        {
            MinimumTier = AccessTier.Verified
        };

        yield return new PermissionDefinition(
            JobPermissions.Moderate, JobsModuleMetadata.Name,
            "Hide, unhide and annotate any job posting or employer.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = StaffRoles
        };

        yield return new PermissionDefinition(
            JobPermissions.ManageCategories, JobsModuleMetadata.Name,
            "Manage the employment-domain catalogue.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };

        yield return new PermissionDefinition(
            JobPermissions.ManageSkills, JobsModuleMetadata.Name, "Manage the skill catalogue.")
        {
            MinimumTier = AccessTier.Limited,
            Roles = AdminRoles
        };
    }
}
