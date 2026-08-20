namespace Diaspora.Modules.Media.Extensibility.Access;

/// <summary>
/// A vertical's verdict on one access request.
/// <para>
/// Three outcomes rather than a boolean, and the third is the one that earns its keep.
/// <see cref="Abstain"/> means "this is not a case I have an opinion about" and leaves Media's
/// own decision standing. Without it, a policy that only cares about floor plans would have to
/// return <c>Allow</c> for every gallery photograph — which reads as a deliberate grant and is
/// indistinguishable from one in an audit.
/// </para>
/// <para>
/// <see cref="Reason"/> is for the log, not the member. A refusal surfaces to a client as a
/// generic "you do not have access to that file"; telling a prober <em>why</em> a specific
/// file was refused is an oracle for what exists.
/// </para>
/// </summary>
public sealed record MediaAccessDecision
{
    private MediaAccessDecision(bool? allowed, string? reason)
    {
        Allowed = allowed;
        Reason = reason;
    }

    /// <summary>True to allow, false to deny, null to abstain.</summary>
    public bool? Allowed { get; }

    /// <summary>Why. Logged; never returned to a caller.</summary>
    public string? Reason { get; }

    public bool IsAbstention => Allowed is null;

    public bool IsDenial => Allowed is false;

    /// <summary>The vertical grants access on its own authority.</summary>
    public static MediaAccessDecision Allow(string? reason = null) => new(true, reason);

    /// <summary>The vertical refuses. Final — Media does not override a denial.</summary>
    public static MediaAccessDecision Deny(string? reason = null) => new(false, reason);

    /// <summary>No opinion. Media's own decision stands.</summary>
    public static MediaAccessDecision Abstain() => new(null, null);
}
