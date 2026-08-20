using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Shares;

/// <summary>Reads and writes shares.</summary>
public interface IShareRepository
{
    Task<int> CountAsync(ContentReference content, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountManyAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Share>> ListForContentAsync(
        ContentReference content, int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Share>> ListByMemberAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default);

    Task AddAsync(Share share, CancellationToken cancellationToken = default);
}
