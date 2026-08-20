namespace Diaspora.Platform.Operations.Policies;

/// <summary>
/// Declares operational policies to the kernel.
/// <para>
/// The same contributor shape B2.6 established for permissions, gates and authorisation policies,
/// for the same reason: a component that needs different retry behaviour says so in its own
/// assembly and the host never learns its name. The Financial Platform declaring a stricter
/// breaker for payment gateways does not touch <c>Program.cs</c>.
/// </para>
/// <para>
/// <b>Declaring is not depending.</b> A business module may declare a policy — it knows what its
/// external dependency costs — without referencing anything that enforces one. The enforcement
/// lives in <c>Diaspora.Operations</c>, which no module references.
/// </para>
/// </summary>
public interface IOperationalPolicyContributor
{
    /// <summary>The declaring module's name, as it appears in the module registry.</summary>
    string Module { get; }

    /// <summary>The policies this module declares. Composed in registration order, then by version.</summary>
    IEnumerable<OperationalPolicy> Policies { get; }
}
