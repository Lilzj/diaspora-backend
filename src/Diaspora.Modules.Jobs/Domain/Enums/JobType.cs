namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// The employment arrangement on offer — the contract, not the place.
/// <para>
/// <b>Remote, Hybrid and On-site are deliberately not members here.</b> The brief lists them
/// alongside the others, but they answer a different question: this enum says what kind of
/// engagement the work is, and <see cref="WorkplaceType"/> says where it happens. Merging them
/// would make "remote full-time contract" inexpressible, which is one of the most common
/// diaspora postings on the platform. The two are orthogonal and are modelled that way.
/// </para>
/// <para>
/// An enum with a reserved <see cref="Other"/> rather than a free-form string, per the
/// convention B5's owner type and B6's content type both set: a string lets any caller invent
/// a value that then appears in indexes, filters and analytics with no way to enumerate what
/// exists.
/// </para>
/// </summary>
public enum JobType
{
    FullTime = 1,

    PartTime = 2,

    Contract = 3,

    Temporary = 4,

    Internship = 5,

    Apprenticeship = 6,

    Freelance = 7,

    /// <summary>Unpaid, and validated as such — a volunteer posting may not carry a salary.</summary>
    Volunteer = 8,

    /// <summary>Seasonal work. Common in hospitality and agriculture postings.</summary>
    Seasonal = 9,

    /// <summary>An arrangement the platform has not catalogued yet.</summary>
    Other = 99
}
