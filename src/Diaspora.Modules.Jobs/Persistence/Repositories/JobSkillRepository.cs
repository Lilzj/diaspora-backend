using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Postings;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Jobs.Persistence.Repositories;

/// <summary>
/// EF Core repository for the skill catalogue.
/// <para>
/// <b>Resolution takes keys before aliases, deliberately.</b> A key and another skill's alias
/// could in principle collide — two unique indexes across two tables do not prevent it — and
/// taking the canonical key first makes the outcome decided rather than undefined.
/// </para>
/// </summary>
public sealed class JobSkillRepository : IJobSkillRepository
{
    private readonly DiasporaDbContext _dbContext;

    public JobSkillRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<JobSkill> Skills => _dbContext.Set<JobSkill>();

    public async Task<JobSkill?> GetAsync(
        JobSkillId id, CancellationToken cancellationToken = default) =>
        await Skills
            .Include(skill => skill.Aliases)
            .FirstOrDefaultAsync(skill => skill.Id == id, cancellationToken);

    public async Task<JobSkill?> ResolveAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var normalized = JobSkillRequirement.NormalizeKey(key);

        if (normalized is null)
        {
            return null;
        }

        // The canonical key first, so a key/alias collision resolves the same way every time.
        var byKey = await Skills
            .Include(skill => skill.Aliases)
            .FirstOrDefaultAsync(skill => skill.Key == normalized, cancellationToken);

        if (byKey is not null)
        {
            return byKey;
        }

        return await Skills
            .Include(skill => skill.Aliases)
            .FirstOrDefaultAsync(
                skill => skill.Aliases.Any(alias => alias.AliasKey == normalized),
                cancellationToken);
    }

    public async Task<IReadOnlyList<JobSkill>> ResolveManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var normalized = keys
            .Select(JobSkillRequirement.NormalizeKey)
            .Where(key => key is not null)
            .Select(key => key!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count == 0)
        {
            return [];
        }

        // One query for a whole posting's skill list, keys and aliases together.
        return await Skills
            .Include(skill => skill.Aliases)
            .Where(skill =>
                normalized.Contains(skill.Key)
                || skill.Aliases.Any(alias => normalized.Contains(alias.AliasKey)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobSkill>> SuggestAsync(
        string? term, int limit, CancellationToken cancellationToken = default)
    {
        var query = Skills.AsNoTracking().Where(skill => skill.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var prefix = term.Trim();

            // Prefix rather than contains: autocomplete on a two-character term that matched
            // anywhere would return most of the catalogue and rank it by nothing useful.
            query = query.Where(skill =>
                EF.Functions.Like(skill.Name, $"{prefix}%")
                || skill.Aliases.Any(alias => EF.Functions.Like(alias.AliasKey, $"{prefix}%")));
        }

        return await query
            .OrderByDescending(skill => skill.UsageCount)
            .ThenBy(skill => skill.Name)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobSkill>> GetPopularAsync(
        string? categoryKey, int limit, CancellationToken cancellationToken = default)
    {
        var query = Skills.AsNoTracking().Where(skill => skill.IsActive);

        var category = categoryKey is null ? null : JobCategory.NormalizeKey(categoryKey);

        if (category is not null)
        {
            query = query.Where(skill => skill.CategoryKey == category);
        }

        return await query
            .OrderByDescending(skill => skill.UsageCount)
            .ThenBy(skill => skill.Name)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(JobSkill skill, CancellationToken cancellationToken = default) =>
        await Skills.AddAsync(skill, cancellationToken);
}
