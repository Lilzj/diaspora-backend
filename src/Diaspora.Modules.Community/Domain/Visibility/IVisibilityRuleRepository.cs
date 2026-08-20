using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Visibility;

/// <summary>Reads and writes stored audience rules.</summary>
public interface IVisibilityRuleRepository
{
    Task<VisibilityRule?> GetAsync(
        VisibilityRuleId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VisibilityRule>> ListForOwnerAsync(
        UserId ownerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<VisibilityRuleId, VisibilityRule>> GetManyAsync(
        IReadOnlyCollection<VisibilityRuleId> ids, CancellationToken cancellationToken = default);

    Task AddAsync(VisibilityRule rule, CancellationToken cancellationToken = default);

    void Remove(VisibilityRule rule);
}
