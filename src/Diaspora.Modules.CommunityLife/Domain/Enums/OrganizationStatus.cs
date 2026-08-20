namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Where an organization sits in its lifecycle.
/// <para>
/// <b>Verified is not a status.</b> The brief draws the lifecycle with verification in the middle
/// of it, and modelling it that way would be wrong twice over: verification is optional, so an
/// organization that skipped it would have to jump the state, and it is revocable, so a decision
/// arriving from Trust would have to rewind a lifecycle it does not own. Verification is a mark
/// carried in <see cref="ValueObjects.OrganizationTrustMark"/> and moves independently — which is
/// what lets a published organization become verified without republishing, and lose the badge
/// without being taken down.
/// </para>
/// <para>
/// <see cref="Active"/> is likewise not a separate act. An organization becomes active by doing
/// something — running an event, admitting a member — and the transition is made by the module
/// rather than requested by an editor. It exists so a directory can tell a living association from
/// a page somebody made once.
/// </para>
/// </summary>
public enum OrganizationStatus
{
    /// <summary>Being written. Visible to its own administrators and nobody else.</summary>
    Draft = 0,

    /// <summary>Submitted for review. Still not discoverable.</summary>
    Submitted = 1,

    /// <summary>Live and discoverable, with nothing yet happening under it.</summary>
    Published = 2,

    /// <summary>Live, discoverable and demonstrably running. Set by the module, not by an editor.</summary>
    Active = 3,

    /// <summary>Removed from discovery by a moderator. Reversible, and the members remain.</summary>
    Suspended = 4,

    /// <summary>Withdrawn by its owners. Reversible only through a restore into draft.</summary>
    Archived = 5
}
