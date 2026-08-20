namespace Diaspora.Modules.Community.Domain.Reactions;

/// <summary>Reads and writes the reaction catalogue.</summary>
public interface IReactionDefinitionRepository
{
    Task<IReadOnlyList<ReactionDefinition>> ListAsync(
        bool includeDisabled = false, CancellationToken cancellationToken = default);

    Task<ReactionDefinition?> FindAsync(
        string key, CancellationToken cancellationToken = default);

    Task AddAsync(ReactionDefinition definition, CancellationToken cancellationToken = default);
}
