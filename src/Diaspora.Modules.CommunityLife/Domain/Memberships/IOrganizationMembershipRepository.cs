using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.CommunityLife.Domain.Memberships;

/// <summary>
/// How the module reads and writes memberships.
/// <para>
/// Every method that answers a question about one member in one organization takes both
/// identifiers, because that pair is the natural key and every authorisation check in the module
/// asks it. There is deliberately no "get all memberships" — a caller that wants to iterate every
/// membership on the platform is about to do something this module should be doing for it.
/// </para>
/// </summary>
public interface IOrganizationMembershipRepository
{
    Task<OrganizationMembership?> GetAsync(
        OrganizationMembershipId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The membership joining one member to one organization, whatever its status.
    /// <para>
    /// Returns ended memberships too, and that is load-bearing: somebody an administrator removed
    /// must not be able to rejoin by creating a second row, and the only way to know is to look at
    /// the one that already exists.
    /// </para>
    /// </summary>
    Task<OrganizationMembership?> FindAsync(
        OrganizationId organizationId,
        UserId memberUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The caller's current standing in an organization, or null when they have none.
    /// <para>
    /// The single most-called method in the module: every event write, every membership decision
    /// and every media change asks it. Current only — an ended membership grants nothing.
    /// </para>
    /// </summary>
    Task<OrganizationMembership?> FindCurrentAsync(
        OrganizationId organizationId,
        UserId memberUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Every organization the member currently belongs to. Bounded, for a picker.</summary>
    Task<IReadOnlyList<OrganizationMembership>> FindCurrentForMemberAsync(
        UserId memberUserId, int limit = 50, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<OrganizationMembership>> SearchAsync(
        MembershipQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>How many members an organization has in each of the given statuses.</summary>
    Task<IReadOnlyDictionary<MembershipStatus, int>> CountByStatusAsync(
        OrganizationId organizationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of an organization's current members, oldest first — B23.x Wave 12.
    /// <para>
    /// <b>Ascending by when they joined, because the past does not change.</b> A descending or
    /// relevance-ordered walk shifts under a member joining mid-page, so somebody is skipped and
    /// somebody is visited twice. That is tolerable in a directory a person is scrolling and is not
    /// tolerable for the caller this grew for — an automation notifying an organization's members —
    /// where the two symptoms are a member who was not told and a member told twice.
    /// </para>
    /// <para>
    /// Offset paging rather than the cursor <see cref="SearchAsync"/> uses, because the caller is a
    /// bounded server-side walk under a ceiling rather than a client scrolling a list, and it holds no
    /// state between pages. Returns identifiers only: a broadcast needs nothing else about a
    /// membership, and returning the aggregate would put a member's request message and their
    /// admission decision inside an automation's working set.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ListCurrentMemberPageAsync(
        OrganizationId organizationId,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>How many members hold at least the given role. The last-owner check.</summary>
    /// <summary>
    /// The member who owns an organization, or null when it has none.
    /// <para>
    /// Exactly one at a time — <see cref="MembershipRole.Owner"/> says so — and every
    /// organization is created with one. Null therefore means something is wrong rather than
    /// something ordinary, and callers should treat it as "nobody to reach" rather than
    /// substituting an administrator on their own authority.
    /// </para>
    /// </summary>
    Task<OrganizationMembership?> FindOwnerAsync(
        OrganizationId organizationId,
        CancellationToken cancellationToken = default);

    Task<int> CountByRoleAsync(
        OrganizationId organizationId,
        MembershipRole role,
        CancellationToken cancellationToken = default);

    /// <summary>Memberships whose renewal window has passed. The lapse sweep's work list.</summary>
    Task<IReadOnlyList<OrganizationMembership>> FindLapsedAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    Task AddAsync(
        OrganizationMembership membership, CancellationToken cancellationToken = default);

    void Update(OrganizationMembership membership);
}
