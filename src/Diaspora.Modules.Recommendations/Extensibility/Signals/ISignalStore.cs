using Diaspora.Modules.Recommendations.Domain.Signals;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Extensibility.Signals;

/// <summary>
/// The one door into the signal record.
/// <para>
/// <b>Every privacy guarantee this module makes is enforced here, which is why nothing else may
/// write a signal.</b> The store checks the member's <c>ShareActivitySignals</c> preference and
/// drops behavioural drafts when it is off; it clamps the weight a client-supplied draft may claim;
/// it stamps the retention expiry every row is swept by. A consumer that constructed the aggregate
/// itself would silently opt a member back in, and no test would fail.
/// </para>
/// <para>
/// <b>Named a store rather than a repository, and the difference is real.</b>
/// <c>ISignalRepository</c> is persistence — add, read, delete. This is the policy layer above it,
/// and it is the seam a deployment would replace to put signals in a time-series database or a
/// stream while leaving every consumer untouched.
/// </para>
/// <para>
/// <b>Dropping is normal and is not an error.</b> A member who turned signal collection off produces
/// drafts that go nowhere, and the count returned says how many survived — a consumer that treated
/// zero as a failure would retry forever against a preference that will not change.
/// </para>
/// </summary>
public interface ISignalStore
{
    /// <summary>Records one draft. Returns false when a preference dropped it.</summary>
    Task<bool> RecordAsync(SignalDraft draft, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a batch, and raises one event for the batch rather than one per row.
    /// <para>
    /// Returns how many were written. See the class summary — fewer than were offered is a normal
    /// outcome.
    /// </para>
    /// </summary>
    Task<int> RecordManyAsync(
        IReadOnlyList<SignalDraft> drafts, CancellationToken cancellationToken = default);

    /// <summary>A member's recent signals, newest first, bounded.</summary>
    Task<IReadOnlyList<UserSignal>> ReadRecentAsync(
        UserId userId, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes everything the platform has observed about a member.
    /// <para>
    /// The GDPR erasure hook. Hard deletes — see <c>UserSignal</c> for why a soft delete would be
    /// the wrong answer to an erasure request.
    /// </para>
    /// </summary>
    Task<int> EraseAsync(UserId userId, CancellationToken cancellationToken = default);
}
