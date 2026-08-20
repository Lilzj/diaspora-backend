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
