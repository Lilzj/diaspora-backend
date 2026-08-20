using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Jobs.Domain.Employers;

/// <summary>
/// Reads and writes employer profiles.
/// <para>
/// <see cref="GetAsync"/> loads members and regions because every command needs the membership
/// to answer "may this caller do this here"; the listing read does not, because a directory card
/// renders a name, a badge and a country.
/// </para>
/// </summary>
public interface IEmployerProfileRepository
{
    /// <summary>The full aggregate, tracked, with members and regions. For commands.</summary>
    Task<EmployerProfile?> GetAsync(
        EmployerProfileId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, untracked. For the profile page.</summary>
    Task<EmployerProfile?> GetForReadAsync(
        EmployerProfileId id, CancellationToken cancellationToken = default);

    /// <summary>The full graph, by the deep-link handle.</summary>
    Task<EmployerProfile?> GetBySlugAsync(
        JobSlug slug, CancellationToken cancellationToken = default);

    /// <summary>Several employers at once, untracked. Hydrates the employer chip on job cards.</summary>
    Task<IReadOnlyList<EmployerProfile>> GetManyAsync(
        IReadOnlyCollection<EmployerProfileId> ids, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every employer this member belongs to, in any role. What the "switch employer" control
    /// on the recruiter dashboard reads, and what an authorisation check resolves against when
    /// the command did not name one.
    /// </summary>
    Task<IReadOnlyList<EmployerProfile>> GetForMemberAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>The employer directory listing.</summary>
    Task<CursorPagedResult<EmployerProfile>> QueryAsync(
        EmployerQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Advisory uniqueness check. The unique index is what actually guarantees it.</summary>
    Task<bool> SlugExistsAsync(JobSlug slug, CancellationToken cancellationToken = default);

    /// <summary>The employer holding Trust's case identifier, tracked. How an outcome lands.</summary>
    Task<EmployerProfile?> GetByVerificationIdAsync(
        Guid verificationId, CancellationToken cancellationToken = default);

    Task AddAsync(EmployerProfile employer, CancellationToken cancellationToken = default);

    void Remove(EmployerProfile employer);
}
