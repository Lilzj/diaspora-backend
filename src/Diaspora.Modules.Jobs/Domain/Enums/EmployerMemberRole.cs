namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// What a member may do inside an employer.
/// <para>
/// <b>Employer membership is not a platform role.</b> These sit alongside the platform's own
/// role and tier system rather than inside it: a member may be an Owner of one employer, a
/// Recruiter at another and nothing at a third, and none of that is expressible as a claim on
/// their token. The employer aggregate answers the question, which is why every employer-scoped
/// command asks it rather than reading a permission.
/// </para>
/// <para>
/// Ordered by authority so a check is a comparison: an Owner may do anything a Recruiter may.
/// </para>
/// </summary>
public enum EmployerMemberRole
{
    /// <summary>May read applications but change nothing. A hiring manager observing.</summary>
    Viewer = 1,

    /// <summary>May post jobs and work applications, but not change the employer itself.</summary>
    Recruiter = 2,

    /// <summary>May do everything a Recruiter may, plus manage members and branding.</summary>
    Administrator = 3,

    /// <summary>Full authority, including transferring or deleting the employer. Never zero.</summary>
    Owner = 4
}
