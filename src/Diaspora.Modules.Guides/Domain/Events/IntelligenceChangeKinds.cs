namespace Diaspora.Modules.Guides.Domain.Events;

/// <summary>
/// The verbs Country Intelligence's domain events carry.
/// <para>
/// Strings rather than an enum, matching <see cref="KnowledgeChangeKinds"/>, because they reach a
/// published integration contract and cross into systems that are not .NET. A routing rule in B13
/// keyed on <c>published</c> is legible to whoever writes the next one; the same rule keyed on
/// <c>3</c> requires them to find this file first.
/// </para>
/// <para>
/// <b>One event per aggregate with a verb on it, rather than an event type per verb.</b> The
/// alternative would give eight handlers per aggregate that all do the same two things: relay a
/// contract and drop a cache. Where the payloads genuinely differ — publication carries an audience
/// and a severity that a rename does not — the relay branches, which is one branch in one file
/// rather than eight subscriptions.
/// </para>
/// </summary>
public static class IntelligenceChangeKinds
{
    public const string Created = "created";

    public const string Updated = "updated";

    /// <summary>Sent for verification. The editorial hand-off.</summary>
    public const string Submitted = "submitted";

    /// <summary>A verifier confirmed the source.</summary>
    public const string Verified = "verified";

    /// <summary>A verifier did not confirm the source.</summary>
    public const string Rejected = "rejected";

    public const string Published = "published";

    /// <summary>Replaced by a later notice, which the event names.</summary>
    public const string Superseded = "superseded";

    /// <summary>Withdrawn. The soft delete.</summary>
    public const string Archived = "archived";

    public const string Restored = "restored";

    /// <summary>An expiry date passed and the sweep retired it.</summary>
    public const string Expired = "expired";
}
