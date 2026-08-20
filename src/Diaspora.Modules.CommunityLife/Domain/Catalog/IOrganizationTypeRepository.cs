using Diaspora.Modules.CommunityLife.Domain.Identifiers;

namespace Diaspora.Modules.CommunityLife.Domain.Catalog;

/// <summary>How the module reads and writes the organization-type catalogue.</summary>
public interface IOrganizationTypeRepository
{
    Task<OrganizationType?> GetAsync(
        OrganizationTypeId id, CancellationToken cancellationToken = default);

    Task<OrganizationType?> FindByKeyAsync(
        string? key, CancellationToken cancellationToken = default);

    /// <summary>The whole catalogue. Small, cached, and read on every picker render.</summary>
    Task<IReadOnlyList<OrganizationType>> ListAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default);

    Task AddAsync(OrganizationType type, CancellationToken cancellationToken = default);

    void Update(OrganizationType type);
}
