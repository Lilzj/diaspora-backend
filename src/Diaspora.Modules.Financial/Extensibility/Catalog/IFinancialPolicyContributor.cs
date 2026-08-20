namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>
/// A module declaring the financial policies it needs.
/// <para>
/// <b>The seam that keeps commission out of the verticals.</b> Marketplace does not compute its own
/// commission and does not hold a rate; it contributes a policy saying one should exist with a
/// starting value, and from then on the number is an operator's to change. A vertical arriving in
/// B16 does the same with no edit here.
/// </para>
/// <para>
/// Seeding is idempotent: a policy whose key and scope already exist is left exactly as it is,
/// because a deployment that has retuned a rate must not have it reset on the next restart.
/// </para>
/// </summary>
public interface IFinancialPolicyContributor
{
    /// <summary>The module contributing. Reported by the startup validator.</summary>
    string Module { get; }

    IEnumerable<FinancialPolicySeed> GetPolicies();
}
