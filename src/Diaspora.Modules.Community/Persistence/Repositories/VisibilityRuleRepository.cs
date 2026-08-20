using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Visibility;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Community.Persistence.Repositories;

/// <summary>EF Core repository for named audiences.</summary>
public sealed class VisibilityRuleRepository : IVisibilityRuleRepository
{
    private readonly DiasporaDbContext _dbContext;

    public VisibilityRuleRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<VisibilityRule> Rules => _dbContext.Set<VisibilityRule>();

    public async Task<VisibilityRule?> GetAsync(
        VisibilityRuleId id, CancellationToken cancellationToken = default) =>
        await Rules
            .Include(rule => rule.Clauses)
            .FirstOrDefaultAsync(rule => rule.Id == id, cancellationToken);

    public async Task<IReadOnlyList<VisibilityRule>> ListForOwnerAsync(
        UserId ownerId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        return await Rules
            .Include(rule => rule.Clauses)
            .Where(rule => rule.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<VisibilityRuleId, VisibilityRule>> GetManyAsync(
        IReadOnlyCollection<VisibilityRuleId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return new Dictionary<VisibilityRuleId, VisibilityRule>();
        }

        var rows = await Rules
            .Include(rule => rule.Clauses)
            .Where(rule => ids.Contains(rule.Id))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(rule => rule.Id);
    }

    public async Task AddAsync(VisibilityRule rule, CancellationToken cancellationToken = default) =>
        await Rules.AddAsync(rule, cancellationToken);

    public void Remove(VisibilityRule rule) => Rules.Remove(rule);
}
