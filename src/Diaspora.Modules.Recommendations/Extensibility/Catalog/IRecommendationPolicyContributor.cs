namespace Diaspora.Modules.Recommendations.Extensibility.Catalog;

/// <summary>
/// Implemented by a module to contribute recommendation policies.
/// <para>
/// <b>The seam a legal or regulatory rule lands on.</b> "Do not recommend regulated financial
/// services to members in this country" is a rule the module that owns those listings knows about
/// and this one never will. A contributor in that assembly declares the row; a rule implementing
/// <c>IRecommendationPolicyRule</c> alongside it enforces it; nothing here changes.
/// </para>
/// <para>
/// Constructed before the container exists: public parameterless constructor, no dependencies.
/// </para>
/// </summary>
public interface IRecommendationPolicyContributor
{
    /// <summary>The contributing module's name. Used to attribute a duplicate-key failure.</summary>
    string Module { get; }

    /// <summary>Every policy this module contributes.</summary>
    IEnumerable<RecommendationPolicySeed> GetPolicies();
}
