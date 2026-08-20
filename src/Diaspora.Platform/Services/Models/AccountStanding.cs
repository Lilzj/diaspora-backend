namespace Diaspora.Platform.Services.Models;

/// <summary>
/// How a member's account stands with the platform, coarsely.
/// <para>
/// <b>The counterpart to <see cref="TrustLevel"/>, and deliberately a different question.</b>
/// <see cref="TrustLevel"/> answers <em>how far has this member got</em> — earned, progressive, and
/// the thing a feature gate reads. This answers <em>is anything wrong</em> — imposed, reversible,
/// and the thing a refusal reads. Merging them would put a suspension on the same ladder as a
/// verification, and a module could then "upgrade" its way past a ban.
/// </para>
/// <para>
/// <b>Coarse on purpose, exactly as <see cref="TrustLevel"/> is.</b> The Trust module's
/// <c>AccountHealth</c> has five rungs and will grow more; a consumer that switched on those would
/// have to be revisited every time Trust invented one. Five values project onto four here, and the
/// projection lives in the module that owns the finer ladder.
/// </para>
/// </summary>
public enum AccountStanding
{
    /// <summary>
    /// Nothing is known about this member's standing.
    /// <para>
    /// The value for a member with no trust profile — which is a real state, not an error: a
    /// consumer asks this on paths where "who is this" is already answered. It is <b>not</b> a
    /// refusal. A member who has just registered has no profile for as long as it takes the
    /// provisioner to run, and refusing them would make registration look broken.
    /// </para>
    /// </summary>
    Unknown = 0,

    /// <summary>Nothing is wrong. Covers Trust's Excellent and Good.</summary>
    Good = 1,

    /// <summary>
    /// The member may read and take part, but not publish or initiate.
    /// <para>
    /// Trust's <c>Restricted</c>. What "initiate" means is the consuming module's judgement — Trust
    /// states the standing, never the consequence.
    /// </para>
    /// </summary>
    Restricted = 2,

    /// <summary>The member does nothing. Trust's <c>Suspended</c>.</summary>
    Suspended = 3,

    /// <summary>
    /// A decision is pending on this member.
    /// <para>
    /// Trust's <c>UnderReview</c>. <b>Not a restriction</b>, and the ordering here must not be read
    /// as severity: a member waiting on a review has had nothing taken from them, and a consumer
    /// that refused on <c>&gt;= Restricted</c> would punish them for the platform's own queue. Any
    /// consumer branching on this enum should name the values it refuses.
    /// </para>
    /// </summary>
    UnderReview = 4
}
