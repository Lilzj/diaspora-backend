namespace Diaspora.Modules.Financial.Extensibility.Payouts;

/// <summary>
/// Where the payout capability matrix comes from.
/// <para>
/// <b>A seam rather than a table, because the matrix has two very different lifecycles.</b> What a
/// provider is technically capable of changes when the provider ships something and is read out of
/// documentation; what a deployment is contractually allowed to use changes when somebody finishes
/// an onboarding call. The shipped source reads configuration, so the second kind of change is a
/// deployment edit rather than a release. A source that queried an operator-managed table would
/// implement this same interface and nothing above it would change.
/// </para>
/// <para>
/// <b>Sources are additive.</b> Every registered source is asked and the rows are concatenated, in
/// the same way <c>IFraudAssessor</c> is additive here — so a deployment can carry a shipped
/// baseline matrix and overlay its own without either having to know about the other. Where two
/// rows describe the same route, <c>PayoutProviderRouter</c> takes the more cautious one; see its
/// summary for why that direction and not the other.
/// </para>
/// </summary>
public interface IPayoutCapabilitySource
{
    /// <summary>Whatever this source calls itself. Recorded in diagnostics, never interpreted.</summary>
    string Name { get; }

    /// <summary>
    /// Every row this source knows about, including <see cref="CapabilitySupport.Unsupported"/> and
    /// <see cref="CapabilitySupport.Unknown"/> ones. Returning only the routable rows would lose
    /// exactly the information the refusal reasons are built from — a router that cannot see an
    /// <c>Unknown</c> row cannot tell a member "nobody has confirmed this" instead of "your country
    /// is not supported".
    /// </summary>
    IReadOnlyList<PayoutCapability> GetCapabilities();
}
