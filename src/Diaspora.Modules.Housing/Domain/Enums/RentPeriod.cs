namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// What period a rent figure is quoted for.
/// <para>
/// <b>Stored beside the amount and never converted.</b> A hostel quotes a night, a student hall
/// quotes a week, a flat quotes a month, and normalising all three to a monthly figure would mean
/// dividing by an average month nobody agrees on and publishing a number no landlord wrote. A
/// price range filter therefore compares like with like or does not compare at all, which is the
/// same rule B10 applies to currency.
/// </para>
/// </summary>
public enum RentPeriod
{
    /// <summary>No period, because no price was disclosed.</summary>
    None = 0,

    PerNight = 1,
    PerWeek = 2,
    PerMonth = 3,
    PerQuarter = 4,
    PerYear = 5,

    /// <summary>The whole stay, for a fixed short let quoted as one figure.</summary>
    PerStay = 6
}
