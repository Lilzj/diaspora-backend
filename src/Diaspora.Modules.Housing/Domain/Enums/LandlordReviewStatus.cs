namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// Where a landlord review stands.
/// <para>
/// The states exist because <c>LandlordReviewSummary</c> is a denormalised rollup, and a rollup
/// has to know which reviews count. Only <see cref="Published"/> does.
/// </para>
/// </summary>
public enum LandlordReviewStatus
{
    /// <summary>Visible, and counted in the rollup.</summary>
    Published = 0,

    /// <summary>Taken down by a moderator. Not counted, not shown, still recoverable.</summary>
    Hidden = 1,

    /// <summary>Taken back by its author. A member must always be able to unsay something.</summary>
    Withdrawn = 2
}
