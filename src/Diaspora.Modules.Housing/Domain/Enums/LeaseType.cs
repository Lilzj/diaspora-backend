namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// The shape of the arrangement, not its legal text.
/// <para>
/// Housing records which kind of stay a listing offers so a newcomer can filter for the six weeks
/// they need before their permanent place. It does not model, store or interpret a tenancy
/// agreement — lease management is explicitly out of scope, and this enum is the boundary marker.
/// </para>
/// </summary>
public enum LeaseType
{
    Unspecified = 0,
    Nightly = 1,
    Weekly = 2,
    MonthToMonth = 3,
    FixedTerm = 4,
    ShortTerm = 5,
    LongTerm = 6,
    AcademicYear = 7,
    Corporate = 8
}
