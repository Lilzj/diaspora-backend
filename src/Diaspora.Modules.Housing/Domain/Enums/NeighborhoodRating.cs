namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// A five-band qualitative rating, used for every liveability index a neighbourhood carries.
/// <para>
/// <b>Bands rather than a 0-100 score, and the choice is about honesty.</b> B4 already carries
/// nullable integer safety and walkability scores against the day a data source exists for them;
/// what this module records is different in kind - an editor's or a community's judgement, which
/// is real to within about a fifth of the range and no further. Publishing "walkability 73" from
/// a human read would be a precision the platform does not have, and a number a client will chart.
/// </para>
/// <para>
/// <see cref="Unknown"/> is the default and is not a low rating. A neighbourhood nobody has
/// described yet must not read as a bad one.
/// </para>
/// </summary>
public enum NeighborhoodRating
{
    Unknown = 0,
    VeryLow = 1,
    Low = 2,
    Moderate = 3,
    High = 4,
    VeryHigh = 5
}
