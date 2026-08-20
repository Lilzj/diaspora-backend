namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Where an official notice sits in its life.
/// <para>
/// <b>Two steps longer than a guide's lifecycle, and both extra steps are the point of the
/// phase.</b> A guide goes from draft to published because its author decided it was ready. A
/// notice passes through <see cref="Submitted"/> and <see cref="Verified"/> because somebody other
/// than the writer confirmed the authority actually said this — which is the entire difference
/// between "a member reports that visa rules changed" and "the immigration department announced a
/// change". Collapsing the two steps would leave the platform publishing unchecked claims under an
/// official badge, which is worse than publishing nothing.
/// </para>
/// <para>
/// <b><see cref="Superseded"/> is not a way of being gone.</b> A superseded notice stays readable
/// forever, pointing at whatever replaced it, because somebody who followed the old rule needs to
/// see what it said and when it stopped being true. Deleting it would leave a dead link in every
/// message the platform ever sent about it.
/// </para>
/// <para>
/// <b><see cref="Archived"/> is the soft delete, and there is deliberately only one gone
/// state.</b> Carrying an archived status <em>and</em> a soft-delete flag would give two ways for a
/// notice to be absent, two queries that disagree about which, and a restore that fixes one of them.
/// B8.1 made the same call for its navigation rows and its country directories.
/// </para>
/// </summary>
public enum NoticeStatus
{
    /// <summary>Being written. Visible only to its editors.</summary>
    Draft = 0,

    /// <summary>Submitted for verification. Locked against content edits.</summary>
    Submitted = 1,

    /// <summary>A verifier confirmed the source. Ready to publish, not yet published.</summary>
    Verified = 2,

    /// <summary>Live and readable by anybody.</summary>
    Published = 3,

    /// <summary>Replaced by a later notice, and pointing at it. Still readable.</summary>
    Superseded = 4,

    /// <summary>Withdrawn. The soft-deleted state; restorable.</summary>
    Archived = 5
}
