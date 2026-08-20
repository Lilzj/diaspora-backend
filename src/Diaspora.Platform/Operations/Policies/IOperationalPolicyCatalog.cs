namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// The composed set of operational policies in force on this host.
/// <para>
/// Resolution is by family and name together, and it is total: <see cref="Get{TPolicy}"/> returns
/// the caller's declared fallback rather than null when nothing is registered. A resilience
/// executor that has to null-check its own policy is an executor that will one day run with no
/// policy at all and no one will notice.
/// </para>
/// </summary>
public interface IOperationalPolicyCatalog
{
    /// <summary>Every policy in force, ordered by kind then name. The dashboard reads this.</summary>
    IReadOnlyList<OperationalPolicy> All { get; }

    /// <summary>
    /// The policy registered under <paramref name="name"/> for family <typeparamref name="TPolicy"/>,
    /// or <paramref name="fallback"/> when none is.
    /// </summary>
    TPolicy Get<TPolicy>(string name, TPolicy fallback) where TPolicy : OperationalPolicy;

    /// <summary>
    /// The policy registered under <paramref name="name"/>, or <see langword="null"/>. For callers
    /// that genuinely want to know whether a deployment declared one — the dashboard, the
    /// validators — rather than callers that need a value to act on.
    /// </summary>
    TPolicy? Find<TPolicy>(string name) where TPolicy : OperationalPolicy;
}
