using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// How the module loads and stores an organization it is about to change.
/// <para>
/// Thirty-four of this interface's forty-five consumer classes call <see cref="GetAsync"/>, and
/// twelve of those call nothing but it and <see cref="Update"/>. <see cref="FindManyAsync"/> is
/// declared here and on the search role for the usual reason: hydrating a batch of ids is the
/// same operation whichever side asked for them.
/// </para>
/// </summary>
public interface IOrganizationRepository
{
    Task<Organization?> GetAsync(
        OrganizationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// An organization by its deep-link handle.
    /// <para>
    /// Resolves soft-deleted pages too when asked, because a link shared in a family group chat
    /// years ago should give the member a "this page has gone" rather than a 404 that looks like a
    /// broken app.
    /// </para>
    /// </summary>
    /// <summary>
    /// An organization including a soft-deleted one.
    /// <para>
    /// <b>The one named member that ignores the delete filter.</b> Every path that has to act on a
    /// row precisely because it is deleted — a restore, a moderator reviewing what was taken down,
    /// a report resolved after the subject was removed — cannot use <see cref="GetAsync"/>: the
    /// global filter hides the row, the handler reads null and answers "not found", and an undelete
    /// that can never find anything to undelete is a feature that fails silently in production
    /// rather than in a test.
    /// </para>
    /// <para>
    /// Named rather than a boolean parameter on <see cref="GetAsync"/> so that reaching past the
    /// filter is a visible decision at the call site.
    /// </para>
    /// </summary>
    Task<Organization?> GetIncludingDeletedAsync(
        OrganizationId id, CancellationToken cancellationToken = default);

    Task<Organization?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default);

    /// <summary>Organizations the member currently belongs to at any role. Bounded, for a picker.</summary>
    Task<IReadOnlyList<Organization>> FindManyAsync(
        IReadOnlyCollection<OrganizationId> ids, CancellationToken cancellationToken = default);

    /// <summary>The organization holding a Trust case. How an outcome finds its subject.</summary>
    Task<Organization?> FindByVerificationAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    Task AddAsync(Organization organization, CancellationToken cancellationToken = default);

    void Update(Organization organization);
}

/// <summary>
/// Discovery: a page of organizations, and the batch load a list of cards hydrates with.
/// </summary>
public interface IOrganizationSearch
{
    Task<CursorPagedResult<Organization>> SearchAsync(
        OrganizationQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Organizations the member currently belongs to at any role. Bounded, for a picker.</summary>
    Task<IReadOnlyList<Organization>> FindManyAsync(
        IReadOnlyCollection<OrganizationId> ids, CancellationToken cancellationToken = default);
}

/// <summary>
/// The questions a create command asks before it writes: is this a duplicate, is the creator
/// within their allowance, is this slug free.
/// </summary>
public interface IOrganizationGuards
{
    /// <summary>Whether the member already runs an organization with that name.</summary>
    Task<bool> HasDuplicateAsync(
        UserId createdByUserId,
        string? name,
        OrganizationId? excluding,
        CancellationToken cancellationToken = default);

    /// <summary>How many live organizations the member created. The abuse bound.</summary>
    Task<int> CountActiveByCreatorAsync(
        UserId createdByUserId, CancellationToken cancellationToken = default);

    /// <summary>Whether a slug is taken. Includes deleted rows — links outlive pages.</summary>
    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default);
}

/// <summary>
/// How many organizations are in a place. What B4's resource counters ask when they compose an
/// area's page.
/// </summary>
public interface IOrganizationStatistics
{
    /// <summary>How many discoverable organizations sit in an area. B4's counter seam.</summary>
    Task<int> CountInAreaAsync(
        string areaPathPrefix, CancellationToken cancellationToken = default);

    Task<int> CountInCountryAsync(
        string countryCode, CancellationToken cancellationToken = default);
}
