using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for the organization-type catalogue.
/// <para>
/// Reference data: small, read on every picker render and on every organization write. The list
/// read is <c>AsNoTracking</c>; the key lookup is tracked, because its caller is about to adjust a
/// count.
/// </para>
/// </summary>
public sealed class OrganizationTypeRepository : IOrganizationTypeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public OrganizationTypeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<OrganizationType> Types => _dbContext.Set<OrganizationType>();

    public async Task<OrganizationType?> GetAsync(
        OrganizationTypeId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Types.FirstOrDefaultAsync(type => type.Id == id, cancellationToken);
    }

    public async Task<OrganizationType?> FindByKeyAsync(
        string? key, CancellationToken cancellationToken = default)
    {
        var normalized = Organization.NormalizeKey(key);

        return normalized is null
            ? null
            : await Types.FirstOrDefaultAsync(type => type.Key == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<OrganizationType>> ListAsync(
        bool includeRetired = false, CancellationToken cancellationToken = default)
    {
        var query = Types.AsNoTracking();

        if (!includeRetired)
        {
            query = query.Where(type => type.IsActive);
        }

        return await query
            .OrderBy(type => type.Order)
            .ThenBy(type => type.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        OrganizationType type, CancellationToken cancellationToken = default) =>
        await Types.AddAsync(type, cancellationToken);

    public void Update(OrganizationType type) => Types.Update(type);
}
