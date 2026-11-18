using Diaspora.SharedKernel.Identity;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Platform.Services.Abstractions;

/// <summary>
/// What any module may ask about a member's trust, without referencing the module that owns it.
/// <para>
/// The kernel declares it; ADR-002 assigns it to B3, which replaces the kernel's placeholder in its
/// own <c>ConfigureServices</c>. A consumer takes a dependency on this assembly and on nothing of
/// Trust's — which is what lets Community rank a feed, Conversations refuse a thread and a future
/// module gate a capability with no project reference between any of them.
/// </para>
/// </summary>
public interface ITrustService
{
    Task<TrustLevel> GetTrustLevelAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The trust level of several members at once.
    /// <para>
    /// <b>Added because the shape of this seam was the defect, not the code that called it.</b> A
    /// feed page is composed from the distinct authors on it, and with only a single-subject method
    /// to ask with, the one way to compose a page was a loop — fifty sequential round trips before
    /// a single post could be ranked, on the application's primary screen, against a serverless
    /// Postgres with real per-query latency. The audit raised that loop twice, as BBL-005 where it
    /// was written and as BARCH-005 where it was caused: <em>the port is the seam, and the seam was
    /// designed for one subject</em>. A caller cannot fix that; only the seam can.
    /// </para>
    /// <para>
    /// <b>Every requested member is in the result, including ones with no profile at all</b>, which
    /// answer <see cref="TrustLevel.None"/> exactly as <see cref="GetTrustLevelAsync"/> does for
    /// them. A dictionary that silently omitted the unknown would make the obvious indexer throw on
    /// the one case callers most often forget, and the two methods would disagree about what a
    /// member with no profile is. Duplicates in <paramref name="userIds"/> are the caller's
    /// convenience, not their problem.
    /// </para>
    /// <para>
    /// <b>A default that loops, and the loop is the point of the test rather than of this
    /// method.</b> There are two implementations in this solution, a placeholder and Trust's, plus
    /// whatever doubles the suites build; making this a required member would break every one of
    /// them to no purpose, since a double looping over three fakes costs nothing. What the default
    /// must not do is hide the finding by moving it one layer down — so Trust's implementation is
    /// held to a single query for any number of members by a test that counts the commands, and
    /// the finding reopens the moment that stops being true.
    /// </para>
    /// </summary>
    async Task<IReadOnlyDictionary<UserId, TrustLevel>> GetTrustLevelsAsync(
        IReadOnlyCollection<UserId> userIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userIds);

        var levels = new Dictionary<UserId, TrustLevel>();

        foreach (var userId in userIds)
        {
            if (levels.ContainsKey(userId))
            {
                continue;
            }

            levels[userId] = await GetTrustLevelAsync(userId, cancellationToken)
                .ConfigureAwait(false);
        }

        return levels;
    }

    Task<bool> IsPublishingEligibleAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether anything is wrong with this member's account.
    /// <para>
    /// <b>Added in PSR-1.2, with a default, and both halves of that matter.</b> Until this existed,
    /// the only questions the kernel could ask were <em>how far has this member got</em> and
    /// <em>may they publish</em> — so a module that wanted to refuse a suspended account had no way
    /// to find out, and the platform's suspensions reached exactly the surfaces Trust's own rule
    /// chain was consulted on. The default returns <see cref="AccountStanding.Unknown"/> rather
    /// than <see cref="AccountStanding.Good"/>: a placeholder that claimed everybody was in good
    /// standing would be plausibly wrong, and the kernel's own <c>DefaultTrustService</c> exists
    /// specifically to be visibly wrong.
    /// </para>
    /// <para>
    /// A default implementation rather than a new member every implementer must write, because the
    /// two implementations in this solution are a placeholder and Trust's, and there is no third
    /// that would benefit from being broken into noticing.
    /// </para>
    /// <para>
    /// <b>This states a fact and never a consequence.</b> What a restriction stops is the calling
    /// module's judgement — refusing a new conversation is not the same decision as refusing a
    /// reply, and Trust is not the place either is made.
    /// </para>
    /// </summary>
    Task<AccountStanding> GetAccountStandingAsync(
        UserId userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(AccountStanding.Unknown);
}
