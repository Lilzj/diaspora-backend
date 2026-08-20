namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Where a visa advisory sits in its life.
/// <para>
/// <b>Shorter than a notice's lifecycle, and the missing steps are the interesting part.</b> An
/// advisory is a standing description of the rules rather than an announcement, so it has no
/// submission queue and no verification gate of its own: what makes it trustworthy is the authority
/// named on it and the official reference it cites, both of which are checked when it is written.
/// A rule change is published as an <c>OfficialNotice</c> — which is what a member is told about —
/// and the advisory is then edited to describe the new rules.
/// </para>
/// <para>
/// <see cref="Archived"/> is the soft delete here too. One gone state per aggregate.
/// </para>
/// </summary>
public enum VisaAdvisoryStatus
{
    /// <summary>Being written. Visible only to its editors.</summary>
    Draft = 0,

    /// <summary>Live and readable by anybody.</summary>
    Published = 1,

    /// <summary>Withdrawn. The soft-deleted state; restorable.</summary>
    Archived = 2
}
