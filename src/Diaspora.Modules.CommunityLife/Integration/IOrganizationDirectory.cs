namespace Diaspora.Modules.CommunityLife.Integration;

/// <summary>
/// The read surface other modules use to look organizations up.
/// <para>
/// <b>This is the contract that stops the next vertical growing its own organization model.</b> B7
/// made the same promise for providers and B11 for neighbourhoods; this keeps it for the bodies
/// members actually belong to. A guide written by an association, a job posted through a student
/// society, a marketplace stall run by a church — each stores an organization identifier and asks
/// here, rather than growing an organization table of its own.
/// </para>
/// <para>
/// Deliberately narrower than <c>IOrganizationRepository</c>. A consumer needs to resolve an
/// organization, render a card and check a badge; it does not need to publish one, suspend one or
/// rewrite its opening hours, and handing it the repository would give it all three. Only
/// discoverable organizations resolve — a consumer holding the identifier of a draft gets null,
/// exactly as it would for one that never existed.
/// </para>
/// <para>
/// <b>Every method degrades rather than throws.</b> A consumer holding a stale identifier is an
/// ordinary state, not an exceptional one.
/// </para>
/// </summary>
public interface IOrganizationDirectory
{
    /// <summary>One organization, or null when it does not exist or is not discoverable.</summary>
    Task<OrganizationSummary?> FindAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>One organization by its deep-link handle.</summary>
    Task<OrganizationSummary?> FindBySlugAsync(
        string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Several at once. The N+1 guard for any screen rendering a list of things that each name an
    /// organization. Absent and undiscoverable organizations are simply missing from the answer.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, OrganizationSummary>> FindManyAsync(
        IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether a member may act on behalf of an organization at a given level of authority.
    /// <para>
    /// The one authorisation question a consuming vertical asks: may this person post a guide as
    /// this association, list a stall as this church. Asking here rather than reimplementing role
    /// comparison is what stops five modules disagreeing about what an administrator may do.
    /// </para>
    /// <para>
    /// <paramref name="minimumRole"/> is the role's name — <c>Member</c>, <c>Volunteer</c>,
    /// <c>Moderator</c>, <c>Organizer</c>, <c>Administrator</c>, <c>Owner</c>. A string rather than
    /// the enum, so a consumer references nothing of this module's; an unrecognised name is treated
    /// as the highest role, which fails closed.
    /// </para>
    /// </summary>
    Task<bool> CanActOnBehalfAsync(
        Guid organizationId,
        Guid userId,
        string minimumRole,
        CancellationToken cancellationToken = default);

    /// <summary>Organizations the member currently belongs to. Bounded, for a picker.</summary>
    Task<IReadOnlyList<OrganizationSummary>> FindForMemberAsync(
        Guid userId, int limit = 20, CancellationToken cancellationToken = default);

    /// <summary>
    /// The member's current standing in one organization, or null when they have none.
    /// <para>
    /// <b>The read behind <see cref="CanActOnBehalfAsync"/>, for a consumer that has to render the
    /// answer rather than compare against it.</b> B18 projects the role into a capability set the
    /// client draws a dashboard from; asking the boolean question ten times would be ten round trips
    /// and would still not tell the screen which role to display.
    /// </para>
    /// <para>
    /// Current memberships only, which is what makes it safe: an ended membership resolves to null
    /// here exactly as it denies there, so the two cannot disagree.
    /// </para>
    /// </summary>
    Task<OrganizationMembershipSummary?> FindMembershipAsync(
        Guid organizationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every current membership the member holds, with its role. Bounded, for a picker.
    /// <para>
    /// The companion to <see cref="FindForMemberAsync"/>, which answers the same question without
    /// the role. Both are kept: a consumer rendering organization cards wants the summaries, and one
    /// rendering an administrator's picker wants the roles.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<OrganizationMembershipSummary>> FindMembershipsForMemberAsync(
        Guid userId, int limit = 20, CancellationToken cancellationToken = default);
}
