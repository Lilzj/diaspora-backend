namespace Diaspora.Modules.Recommendations.Extensibility.Catalog;

/// <summary>
/// Implemented by a module to contribute ranking-strategy configuration.
/// <para>
/// <b>The seam that lets a vertical tune its own surface without this module knowing it exists.</b>
/// Housing knows that proximity matters more than recency for a flat and that a verified landlord
/// matters more than either; the recommendation platform does not and should not. A contributor in
/// the Housing assembly can say so, and this module discovers it the same way B13 discovers a
/// notification template.
/// </para>
/// <para>
/// Constructed by the module before the container exists: public parameterless constructor, no
/// dependencies, no state.
/// </para>
/// </summary>
public interface IRankingStrategyContributor
{
    /// <summary>The contributing module's name. Used to attribute a duplicate-key failure.</summary>
    string Module { get; }

    /// <summary>Every strategy configuration this module contributes.</summary>
    IEnumerable<RankingStrategySeed> GetStrategies();
}
