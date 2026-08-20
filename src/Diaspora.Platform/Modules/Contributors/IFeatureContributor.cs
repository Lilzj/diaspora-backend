using Diaspora.Contracts.Authorization;

namespace Diaspora.Platform.Modules.Contributors;

/// <summary>
/// Implemented by a module to declare the feature gates it owns.
/// <para>
/// B2 declared <c>marketplace.listings.create</c>, <c>jobs.apply</c> and
/// <c>guides.publish</c> inside the Identity module, for modules that did not exist yet.
/// This interface is where they belong instead: with the module that can actually say what
/// the gate protects and when it should open.
/// </para>
/// </summary>
public interface IFeatureContributor
{
    /// <summary>The owning module's name.</summary>
    string Module { get; }

    /// <summary>
    /// Every gate this module owns. Names must be unique across the platform.
    /// </summary>
    IEnumerable<FeatureDefinition> GetFeatures();
}
