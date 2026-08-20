using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Domain.Signals;

/// <summary>
/// The signal store, as the domain sees it.
/// <para>
/// <b>Every method here is either an append or a bounded read, and there is no update.</b> That is
/// the immutability of <see cref="UserSignal"/> expressed as a port rather than as a comment: a
/// repository with no <c>Update</c> cannot be used to edit history, whatever a future handler
/// intends.
/// </para>
/// <para>
/// <b>The deletion methods are deliberate and named for their reasons.</b>
/// <see cref="DeleteExpiredAsync"/> is retention; <see cref="DeleteForUserAsync"/> is erasure. They
/// do the same thing to different rows and conflating them would mean an erasure request that
/// honoured the retention window instead of the request.
/// </para>
/// </summary>
public interface ISignalRepository
{
    void Add(UserSignal signal);

    void AddRange(IEnumerable<UserSignal> signals);

    /// <summary>
    /// A member's signals, newest first, bounded.
    /// <para>
    /// Bounded rather than paged: no caller wants a member's whole history, and the profile builder
    /// wants exactly the most recent window. An unbounded read here would be the module's first
    /// out-of-memory failure on its most active member.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UserSignal>> ListForUserAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Signals the incremental builder has not folded in yet, oldest first.</summary>
    Task<IReadOnlyList<UserSignal>> ListUnprocessedAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>Members with unprocessed signals, for the refresh sweep. Oldest signal first.</summary>
    Task<IReadOnlyList<UserId>> ListUsersWithUnprocessedSignalsAsync(
        int limit, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserSignal>> ListForUserSinceAsync(
        UserId userId, DateTimeOffset since, int limit, CancellationToken cancellationToken = default);

    /// <summary>The member's most recent signal of a kind, when one exists.</summary>
    Task<UserSignal?> FindLatestAsync(
        UserId userId, SignalKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Targets the member has already engaged with, for the "not again" checks.
    /// <para>
    /// Returns the raw identifiers rather than the signals, because the caller builds a set and the
    /// aggregates would be loaded to be discarded.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ListEngagedTargetIdsAsync(
        UserId userId,
        RecommendationTargetType targetType,
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> CountForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>How recent the member's newest signal is. Feeds the freshness measure.</summary>
    Task<DateTimeOffset?> FindLatestOccurrenceAsync(
        UserId userId, CancellationToken cancellationToken = default);

    /// <summary>Retention. Deletes rows whose window has closed, in bounded batches.</summary>
    Task<int> DeleteExpiredAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Erasure. Deletes everything the platform observed about one member.</summary>
    Task<int> DeleteForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
