namespace Diaspora.Application.Abstractions;

/// <summary>
/// Marks a query handler that deliberately writes.
/// <para>
/// <b>The platform's rule is that a query does not mutate state, and the platform has four
/// handlers that do.</b> Each of them is a read-path counter — asking for a listing, a job, an
/// event or a home records that it was viewed — and each is a decision somebody made on purpose,
/// for a reason that is still good. B1.4's guardrail could have been written to ignore them, and
/// then it would have ignored the next one too.
/// </para>
/// <para>
/// So the rule stands and the exceptions declare themselves. <c>DSP4001</c> fires on any query
/// handler that saves or stages without this attribute, which means a write introduced into a
/// query by accident fails the build, and a write introduced on purpose costs one line and leaves
/// a sentence explaining it at the top of the file.
/// </para>
/// <para>
/// This is not a suppression. A suppression says "do not tell me"; this says "I know, and here is
/// why" — and the difference shows up when somebody greps for what the read path writes.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SideEffectingAttribute : Attribute
{
    /// <summary>
    /// What the handler writes, and why it belongs on the read path. Required in spirit if not by
    /// the compiler.
    /// </summary>
    public string? Reason { get; init; }
}
