namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// The formal education a posting requires, or a candidate holds.
/// <para>
/// Ordered, so "a bachelor's or above" is a comparison. <see cref="None"/> is a real and
/// deliberate value rather than an absent one: a posting that genuinely requires no
/// qualification should say so, because a blank field reads as an oversight and quietly
/// discourages exactly the applicants a skilled-trades posting wants.
/// </para>
/// </summary>
public enum EducationLevel
{
    /// <summary>No formal qualification required. Stated, not left blank.</summary>
    None = 0,

    /// <summary>Secondary schooling.</summary>
    SecondarySchool = 1,

    /// <summary>A trade or vocational certificate.</summary>
    Vocational = 2,

    /// <summary>A two-year or equivalent post-secondary award.</summary>
    Associate = 3,

    Bachelor = 4,

    Master = 5,

    Doctorate = 6,

    /// <summary>A licensed professional qualification — medicine, law, accountancy.</summary>
    Professional = 7
}
