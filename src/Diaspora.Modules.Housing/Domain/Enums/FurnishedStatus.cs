namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// How much of the home comes with it.
/// <para>
/// Ordered from least to most, so "at least part-furnished" is a range comparison rather than a
/// hand-maintained set. It is the single most consequential filter for somebody arriving in a
/// country with two suitcases.
/// </para>
/// </summary>
public enum FurnishedStatus
{
    Unfurnished = 0,
    PartlyFurnished = 1,
    Furnished = 2,
    FullyEquipped = 3
}
