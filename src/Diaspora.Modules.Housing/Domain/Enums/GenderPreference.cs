namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// Who a shared household is looking for.
/// <para>
/// <b>Meaningful only on shared accommodation, and the aggregate enforces that.</b> A whole
/// property advertised to one gender is a housing-discrimination problem in most of the
/// jurisdictions this platform serves; a spare room in somebody's own flat is a widely lawful
/// exception. The distinction is not cosmetic and is not left to the client — a gender preference
/// on a non-shared listing is refused.
/// </para>
/// </summary>
public enum GenderPreference
{
    /// <summary>No preference. The default, and what every whole-property listing must carry.</summary>
    Any = 0,

    MaleOnly = 1,
    FemaleOnly = 2,

    /// <summary>A family household. Not a gender restriction, and permitted anywhere.</summary>
    FamilyOnly = 3
}
