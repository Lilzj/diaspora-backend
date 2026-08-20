using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Integration;

/// <summary>
/// The one write another module may perform against B12's role edge.
/// <para>
/// <b>Deliberately not a method on <see cref="IOrganizationDirectory"/>.</b> That contract's whole
/// claim is that it returns "nothing a consumer could write through", and adding a write to it would
/// make the sentence false everywhere it is read. This is a separate port, named for what it does, so
/// a reviewer looking at a module's dependencies can see that it changes roles.
/// </para>
/// <para>
/// <b>B12 remains the only writer, and every rule stays here.</b> The implementation dispatches B12's
/// own <c>ChangeMemberRoleCommand</c>, so the last-owner rule, the "you cannot grant a role above your
/// own" rule, the audit entry and the domain event all happen exactly as they do when B12's own route
/// is called. A consumer that reimplemented any of those would be the second place they are enforced,
/// and the second place is the one that forgets.
/// </para>
/// <para>
/// <b>Why it exists at all.</b> B18 serves the route the Phase 12 register proposes —
/// <c>POST /communities/organizations/{id}/members/{userId}/role</c> — which is keyed by the member,
/// because a client holding a member roll has a user identifier and not B12's membership row. B12's
/// own route is keyed by the row. The translation is B18's; the write is not.
/// </para>
/// </summary>
public interface IOrganizationRoleAdministrator
{
    /// <summary>
    /// Changes a member's role, applying every rule B12's own route applies.
    /// </summary>
    /// <param name="organizationId">The organization.</param>
    /// <param name="membershipId">
    /// B12's membership row, which the caller resolves through
    /// <see cref="IOrganizationDirectory.FindMembershipAsync"/>.
    /// </param>
    /// <param name="role">
    /// <c>Member</c>, <c>Volunteer</c>, <c>Moderator</c>, <c>Organizer</c>, <c>Administrator</c> or
    /// <c>Owner</c>. An unrecognised name is refused.
    /// </param>
    /// <returns>
    /// The failure B12 produced, passed through verbatim. Restating it in the consumer's vocabulary
    /// would mean two error catalogues describing one rule.
    /// </returns>
    Task<Result> ChangeRoleAsync(
        Guid organizationId,
        Guid membershipId,
        string role,
        CancellationToken cancellationToken = default);
}
