using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Reactions;

/// <summary>Reads and writes reaction instances and their per-content summaries.</summary>
public interface IReactionRepository
{
    Task<Reaction?> FindAsync(
        ContentReference content,
        UserId userId,
        string reactionKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Reaction>> ListForViewerAsync(
        IReadOnlyCollection<ContentReference> contents,
        UserId userId,
        CancellationToken cancellationToken = default);

    Task<ReactionSummary?> GetSummaryAsync(
        ContentReference content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Summaries for many items at once. The batch form is not an optimisation but a
    /// requirement: a feed page renders twenty cards, and per-card summary lookups are what make
    /// a feed twenty round trips instead of two.
    /// </summary>
    Task<IReadOnlyDictionary<string, ReactionSummary>> GetSummariesAsync(
        IReadOnlyCollection<ContentReference> contents,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        ContentReference content, string reactionKey, CancellationToken cancellationToken = default);

    Task AddAsync(Reaction reaction, CancellationToken cancellationToken = default);

    Task AddSummaryAsync(ReactionSummary summary, CancellationToken cancellationToken = default);

    void Remove(Reaction reaction);
}
