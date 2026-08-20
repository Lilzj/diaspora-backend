namespace Diaspora.Modules.Guides.Extensibility.Intelligence;

/// <summary>
/// What one validator thinks of a source.
/// <para>
/// <b>Three outcomes rather than a boolean, because a validator that has no opinion is the common
/// case.</b> A deployment's rule about <c>.gov</c> domains has nothing to say about a notice whose
/// source is a printed gazette with no URL, and forcing it to answer true or false would make silence
/// indistinguishable from approval. <see cref="Abstain"/> is what most validators return most of the
/// time.
/// </para>
/// <para>
/// <b>Any single rejection refuses the source; approvals do not outvote it.</b> A source policy is a
/// list of things that must not happen rather than a poll, and a deployment that has said "never
/// accept an immigration notice from a domain we do not recognise" means it whatever else agrees.
/// </para>
/// </summary>
public sealed record SourceValidationVerdict
{
    private SourceValidationVerdict(bool rejected, string? reason)
    {
        IsRejected = rejected;
        Reason = reason;
    }

    /// <summary>Whether this validator refuses the source.</summary>
    public bool IsRejected { get; }

    /// <summary>Why, in words an editor can act on. Logged, and returned in the problem detail.</summary>
    public string? Reason { get; }

    /// <summary>This validator has no opinion. The common case.</summary>
    public static SourceValidationVerdict Abstain() => new(rejected: false, reason: null);

    /// <summary>This validator recognises and accepts the source.</summary>
    public static SourceValidationVerdict Accept() => new(rejected: false, reason: null);

    /// <summary>This validator refuses the source, and says why.</summary>
    public static SourceValidationVerdict Reject(string reason) => new(rejected: true, reason);
}
