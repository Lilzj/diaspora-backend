namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// Why somebody reported a job advert.
/// <para>
/// <b>Two of these are about money leaving a newcomer, and they are first for that reason.</b>
/// Recruitment fraud is the harm this queue exists for: an advert that asks a candidate to pay
/// for a visa, a training course, a uniform or a background check takes the money of somebody who
/// has just arrived, has no income yet and has nobody to ask whether this is normal. By the time
/// they understand, the advert is gone and so is the employer.
/// </para>
/// <para>
/// The rest are the ordinary ways a listing is wrong. They matter, and none of them empties a
/// bank account this week.
/// </para>
/// </summary>
public enum JobReportReason
{
    /// <summary>Not a real job. The "employer" is collecting something and giving nothing.</summary>
    Scam = 0,

    /// <summary>
    /// A candidate was asked to pay. Distinct from <see cref="Scam"/> because it is specific and
    /// provable: no legitimate employer on this platform charges somebody to be hired.
    /// </summary>
    FeeRequested = 1,

    /// <summary>Barring people by nationality, race, age or anything else that is not the work.</summary>
    Discriminatory = 2,

    /// <summary>The pay, the hours or the role are not what the advert said.</summary>
    Misleading = 3,

    /// <summary>The post is still up and the job has gone.</summary>
    AlreadyFilled = 4,

    /// <summary>Posted in the name of a company that did not post it.</summary>
    NotTheEmployer = 5,

    /// <summary>The same opening, already advertised.</summary>
    Duplicate = 6,

    /// <summary>Content that does not belong on the platform.</summary>
    Offensive = 7,

    /// <summary>Something else. The detail is the report.</summary>
    Other = 99
}
