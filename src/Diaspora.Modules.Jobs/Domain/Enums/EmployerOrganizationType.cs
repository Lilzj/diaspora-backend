namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// What kind of organisation is hiring.
/// <para>
/// <b>The distinction that earns its place is <see cref="RecruitmentAgency"/>.</b> A candidate
/// reading a posting is entitled to know whether they are dealing with the employer or with an
/// intermediary, because it changes what questions to ask and what the salary figure means.
/// Job boards that hide it are the reason candidates distrust job boards.
/// </para>
/// <para>
/// An agency posting on behalf of a client is also the case where B7 matters: the agency itself
/// is a Provider, and the posting carries a reference to it rather than a copy of its details.
/// </para>
/// </summary>
public enum EmployerOrganizationType
{
    /// <summary>An established company hiring for itself.</summary>
    Company = 1,

    /// <summary>An early-stage company hiring for itself.</summary>
    Startup = 2,

    /// <summary>A recruiter hiring on behalf of a client. Disclosed, always.</summary>
    RecruitmentAgency = 3,

    /// <summary>A staffing or outsourcing firm placing workers.</summary>
    StaffingFirm = 4,

    NonProfit = 5,

    /// <summary>A government department or public body.</summary>
    Government = 6,

    /// <summary>A school, college or university.</summary>
    Educational = 7,

    /// <summary>A hospital, clinic or care provider.</summary>
    Healthcare = 8,

    /// <summary>A sole trader or household hiring directly.</summary>
    Individual = 9,

    Other = 99
}
