using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// The organization lifecycle, as a machine rather than as a set of scattered checks.
/// <para>
/// <b>Every transition in the module goes through <c>Organization.MoveTo</c>, which asks here.</b>
/// The alternative — each command deciding for itself what it may move from — is how a module ends
/// up able to publish an archived organization from one endpoint and not another.
/// </para>
/// <para>
/// <b><see cref="OrganizationStatus.Suspended"/> returns to Published rather than to whatever it
/// came from</b>, and that is deliberate. A suspension is a moderator's decision about the page as
/// it stands; lifting it should put the page back in front of members, not restore an
/// <see cref="OrganizationStatus.Active"/> flag the organization has not earned again. Activity is
/// re-derived the next time something happens.
/// </para>
/// </summary>
public static class OrganizationStatusTransitions
{
    private static readonly Dictionary<OrganizationStatus, OrganizationStatus[]> Allowed = new()
    {
        [OrganizationStatus.Draft] =
        [
            OrganizationStatus.Submitted,
            OrganizationStatus.Published,
            OrganizationStatus.Archived
        ],
        [OrganizationStatus.Submitted] =
        [
            OrganizationStatus.Draft,
            OrganizationStatus.Published,
            OrganizationStatus.Archived
        ],
        [OrganizationStatus.Published] =
        [
            OrganizationStatus.Active,
            OrganizationStatus.Suspended,
            OrganizationStatus.Archived,
            OrganizationStatus.Draft
        ],
        [OrganizationStatus.Active] =
        [
            OrganizationStatus.Published,
            OrganizationStatus.Suspended,
            OrganizationStatus.Archived
        ],
        [OrganizationStatus.Suspended] =
        [
            OrganizationStatus.Published,
            OrganizationStatus.Archived
        ],

        // Archive is a one-way door out of discovery. The only way back is a restore into Draft,
        // which forces somebody to look at the page before members see it again.
        [OrganizationStatus.Archived] = [OrganizationStatus.Draft]
    };

    public static bool CanTransition(OrganizationStatus from, OrganizationStatus to) =>
        Allowed.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    /// <summary>Whether the organization is reachable by anybody but its own administrators.</summary>
    public static bool IsDiscoverable(OrganizationStatus status) =>
        status is OrganizationStatus.Published or OrganizationStatus.Active;

    /// <summary>Whether members may currently join or be invited.</summary>
    public static bool AcceptsMembership(OrganizationStatus status) =>
        status is OrganizationStatus.Published or OrganizationStatus.Active;

    /// <summary>Whether events may be created under it.</summary>
    public static bool AcceptsEvents(OrganizationStatus status) =>
        status is OrganizationStatus.Published or OrganizationStatus.Active;
}
