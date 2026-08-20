namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// The geographic grain a location affinity is measured at.
/// <para>
/// Three grains rather than one because they answer different questions and decay at different
/// rates. A member's country is close to permanent; their neighbourhood changes when they move flat
/// and is the strongest predictor there is while it lasts. Storing only the finest grain would mean
/// a member who moves city loses every location signal they had.
/// </para>
/// </summary>
public enum AffinityScope
{
    Country = 0,
    City = 1,
    Neighborhood = 2
}
