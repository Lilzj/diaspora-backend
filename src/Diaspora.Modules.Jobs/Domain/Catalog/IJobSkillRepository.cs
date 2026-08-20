using Diaspora.Modules.Jobs.Domain.Identifiers;

namespace Diaspora.Modules.Jobs.Domain.Catalog;

/// <summary>Reads and writes the skill catalogue.</summary>
public interface IJobSkillRepository
{
    Task<JobSkill?> GetAsync(JobSkillId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One skill by its key or by any of its aliases, tracked.
    /// <para>
    /// Resolving aliases here rather than at the call site is what makes "React" and "ReactJS"
    /// one skill everywhere in the module rather than everywhere a caller remembered to check.
    /// </para>
    /// </summary>
    Task<JobSkill?> ResolveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Several by key or alias, tracked. Resolves a whole posting's skill list in one pass.</summary>
    Task<IReadOnlyList<JobSkill>> ResolveManyAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);

    /// <summary>Autocomplete. Prefix-matched against names and aliases, most-used first.</summary>
    Task<IReadOnlyList<JobSkill>> SuggestAsync(
        string? term, int limit, CancellationToken cancellationToken = default);

    /// <summary>The most-used skills, optionally within one employment domain. The chip cloud.</summary>
    Task<IReadOnlyList<JobSkill>> GetPopularAsync(
        string? categoryKey, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(JobSkill skill, CancellationToken cancellationToken = default);
}
