using Diaspora.Contracts.Authorization;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Location.Authorization;

/// <summary>
/// Declares the Location module's feature gates.
/// <para>
/// Gates tell a client what to show; the permissions decide what the server will do. Two of
/// these are gates the client genuinely needs rather than gates for their own sake:
/// <c>location.map</c> and <c>location.nearby</c> both depend on the deployment's spatial
/// provider being able to answer, and a client that renders a map control which cannot work
/// is worse than one that hides it.
/// </para>
/// </summary>
public sealed class LocationFeatureContributor : IFeatureContributor
{
    public string Module => LocationModuleMetadata.Name;

    public IEnumerable<FeatureDefinition> GetFeatures()
    {
        yield return new FeatureDefinition(
            LocationFeatures.Discovery, Module,
            "Browse the platform by place.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            LocationFeatures.Nearby, Module,
            "See what is near you across every part of the platform.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            LocationFeatures.Map, Module,
            "See results on a map.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            LocationFeatures.NeighborhoodHub, Module,
            "Open a neighbourhood's own page.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            LocationFeatures.SavedLocations, Module,
            "Save the places you go — home, work, anywhere.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            LocationFeatures.TravelMode, Module,
            "Discover where you are travelling without losing where you live.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            LocationFeatures.CountryExperience, Module,
            "Record the countries you have lived in.")
        {
            MinimumTier = AccessTier.Limited
        };

        yield return new FeatureDefinition(
            LocationFeatures.CountryPage, Module,
            "Open a country's community page.")
        {
            MinimumTier = AccessTier.Limited
        };
    }
}
