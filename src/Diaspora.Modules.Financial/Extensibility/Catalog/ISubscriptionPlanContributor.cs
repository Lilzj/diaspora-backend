namespace Diaspora.Modules.Financial.Extensibility.Catalog;

/// <summary>
/// A module declaring subscription plans.
/// <para>
/// Plans are not hard-coded. The free plan ships as a system plan so that "no subscription" and
/// "the free plan" are the same answer rather than two, and everything above it is a catalogue row.
/// </para>
/// </summary>
public interface ISubscriptionPlanContributor
{
    string Module { get; }

    IEnumerable<SubscriptionPlanSeed> GetPlans();
}
