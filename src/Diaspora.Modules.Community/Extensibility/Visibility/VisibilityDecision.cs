namespace Diaspora.Modules.Community.Extensibility.Visibility;

/// <summary>
/// An evaluator's answer.
/// <para>
/// Three outcomes rather than two, and the third is what makes composing evaluators tractable.
/// <see cref="Abstain"/> means "I have no opinion", which is what most evaluators say about most
/// content; without it every evaluator would have to return allow for everything it did not care
/// about, and a single evaluator could then override a denial by being indifferent.
/// </para>
/// <para>
/// <b>Composition is deny-wins.</b> Any evaluator denying is decisive; abstentions are ignored;
/// content is visible only if nothing denied. That asymmetry is the safety property: a broken
/// evaluator can hide content that should have been visible, which is a bug somebody reports —
/// it cannot expose content that should have been hidden, which is a breach nobody notices.
/// </para>
/// </summary>
public sealed record VisibilityDecision
{
    private VisibilityDecision(bool denied, bool abstained, string? reason)
    {
        Denied = denied;
        Abstained = abstained;
        Reason = reason;
    }

    public bool Denied { get; }

    public bool Abstained { get; }

    /// <summary>For logs and staff tooling. Never returned to the reader — see the errors file.</summary>
    public string? Reason { get; }

    public static VisibilityDecision Allow { get; } = new(false, false, null);

    public static VisibilityDecision Abstain { get; } = new(false, true, null);

    public static VisibilityDecision Deny(string reason) => new(true, false, reason);
}
