using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Integration;

/// <summary>Reactions, for a module that owns content.</summary>
public interface ICommunityReactionService
{
    Task<Result> ReactAsync(
        ContentReference content,
        UserId userId,
        string reactionKey,
        CancellationToken cancellationToken = default);

    Task<Result> WithdrawAsync(
        ContentReference content,
        UserId userId,
        string reactionKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> GetCountsAsync(
        ContentReference content, CancellationToken cancellationToken = default);
}
