using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Community.Domain.Mentions;

/// <summary>Reads and writes mention records.</summary>
public interface IMentionRepository
{
    Task<CursorPagedResult<Mention>> ListForTargetAsync(
        MentionTargetType targetType,
        Guid targetId,
        FeedCursor? cursor,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Mention>> ListForContentAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    Task AddAsync(Mention mention, CancellationToken cancellationToken = default);

    void RemoveRange(IEnumerable<Mention> mentions);
}
