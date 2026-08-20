using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for the reaction catalogue.</summary>
public sealed class ReactionDefinitionRepository : IReactionDefinitionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ReactionDefinitionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ReactionDefinition> Definitions => _dbContext.Set<ReactionDefinition>();

    public async Task<IReadOnlyList<ReactionDefinition>> ListAsync(
        bool includeDisabled = false, CancellationToken cancellationToken = default)
    {
        var query = Definitions.AsQueryable();

        if (!includeDisabled)
        {
            query = query.Where(definition => definition.IsEnabled);
        }

        return await query
            .OrderBy(definition => definition.Position)
            .ThenBy(definition => definition.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task<ReactionDefinition?> FindAsync(
        string key, CancellationToken cancellationToken = default) =>
        await Definitions.FirstOrDefaultAsync(
            definition => definition.Key == key, cancellationToken);

    public async Task AddAsync(
        ReactionDefinition definition, CancellationToken cancellationToken = default) =>
        await Definitions.AddAsync(definition, cancellationToken);
}
