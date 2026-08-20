namespace Diaspora.Modules.Location;

/// <summary>The authorisation policies the Location module's endpoints reference.</summary>
public static class LocationPolicies
{
    public const string ReadCatalog = "location.catalog.read";
    public const string Search = "location.search";
    public const string Hyperlocal = "location.hyperlocal";
    public const string ReadProfile = "location.profile.read";
    public const string ManageProfile = "location.profile.manage";
    public const string ReadExperience = "location.experience.read";
    public const string ManageExperience = "location.experience.manage";
    public const string ReadPublic = "location.read-public";
    public const string ManageCatalog = "location.catalog.manage";
}
