using Diaspora.SharedKernel.Identity;

namespace Diaspora.Platform.Communication;

/// <summary>
/// The module that owns a subject answers who may listen to it, and who should be told about it.
/// <para>
/// <b>Why this lives in the kernel rather than in the Communication Platform.</b> B13 published
/// <c>IRealtimeGroupAuthorizer</c> as a single-implementation seam, and B13.2 used it exactly as
/// documented — the Conversations Platform registers its own after B13 and wins on
/// last-registration. That works for one vertical and only one. The second module to want a
/// real-time channel would register a third implementation, silently replace the conversation
/// authorizer, and every conversation group on the platform would stop being joinable with nothing
/// logged and no test failing. A seam that only one caller may use is not an extension point.
/// </para>
/// <para>
/// <b>Prefix-scoped, so contributors compose instead of competing.</b> Each implementation claims one
/// channel-name prefix and answers only for names beneath it. The registry refuses two contributors
/// claiming one prefix at startup — the same decision <c>NotificationChannelRegistry</c> makes about
/// two channels claiming one kind, and for the same reason: picking whichever the container ordered
/// first is a difference nobody finds until members report silence.
/// </para>
/// <para>
/// <b>It is in <c>Diaspora.Platform</c> because module order forbids anywhere else.</b> Community
/// Life registers at order 1100 and the Communication Platform at 1200 — B13 consumes what B12
/// publishes, so an edge from B12 to B13 would invert the graph B2.6 exists to keep acyclic.
/// Every module already references the kernel, so a vertical of any order can answer a question
/// about its own data without either module learning about the other.
/// </para>
/// <para>
/// <b>Deny by default, in both senses.</b> A channel whose prefix no contributor claims is refused,
/// and a contributor that throws is treated as having said no — the safe direction for an
/// authorisation question is always "no", and the hub treats a refusal as an ordinary answer rather
/// than a fault.
/// </para>
/// </summary>
public interface IRealtimeChannelAuthorizer
{
    /// <summary>The owning module's name. Used in the duplicate-prefix message and in telemetry.</summary>
    string Module { get; }

    /// <summary>
    /// The channel-name prefix this contributor owns — <c>conversation:</c>, <c>organization:</c>.
    /// <para>
    /// Must end with the separator. A prefix of <c>event</c> rather than <c>event:</c> would also
    /// claim <c>eventual:</c>, which is the kind of overlap that only shows up once a second module
    /// picks a name that happens to start with somebody else's.
    /// </para>
    /// </summary>
    string ChannelPrefix { get; }

    /// <summary>
    /// Whether <paramref name="userId"/> may receive this channel's traffic.
    /// <para>
    /// Consulted on every join, so it must be cheap or cached. Asked, never told: returning false is
    /// an ordinary answer and must not throw.
    /// </para>
    /// </summary>
    /// <param name="userId">The authenticated caller. Never a value the client supplied.</param>
    /// <param name="channel">The full channel name, prefix included.</param>
    Task<bool> CanJoinAsync(
        UserId userId, string channel, CancellationToken cancellationToken = default);

    /// <summary>
    /// The members a message addressed to this channel should reach, at most
    /// <paramref name="limit"/> of them.
    /// <para>
    /// <b>Bounded by contract.</b> A real-time write to a channel reaches whoever happens to be
    /// joined and costs nothing per absentee; a notification does not, and an organization with forty
    /// thousand members must not be able to generate forty thousand inbox rows inside one consumed
    /// event. A contributor that cannot answer cheaply returns an empty list, which means "nobody is
    /// notified" rather than "everybody is" — the safe direction again.
    /// </para>
    /// <para>
    /// The default returns nothing, so a contributor that only gates a live channel implements one
    /// method.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<UserId>> ListSubscribersAsync(
        string channel, int limit, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<UserId>>([]);

    /// <summary>
    /// Whether <paramref name="observer"/> may be told about <paramref name="subject"/>'s
    /// availability.
    /// <para>
    /// <b>Unioned across contributors rather than owned by one</b>, because presence is a property of
    /// a person and not of a channel: sharing a conversation and sharing an organization are two
    /// independent reasons to be allowed to see somebody's dot, and neither prefix owns the question.
    /// One contributor saying yes is enough; all of them saying no is a refusal.
    /// </para>
    /// <para>
    /// The default refuses, so a contributor that has no opinion about presence does not have to
    /// pretend to.
    /// </para>
    /// </summary>
    Task<bool> CanObservePresenceAsync(
        UserId observer, UserId subject, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
