namespace Diaspora.Modules.Location;

/// <summary>
/// The permissions the Location module owns.
/// <para>
/// The geographic catalogue is deliberately readable from the lowest authenticated tier.
/// A member who has not finished onboarding still has to pick the country they live in, and
/// a catalogue they cannot read is an onboarding flow that cannot complete.
/// </para>
/// </summary>
public static class LocationPermissions
{
    /// <summary>Read the geographic catalogue: countries, areas, hierarchy.</summary>
    public const string ReadCatalog = "location:catalog:read";

    /// <summary>Search and geocode: autocomplete, reverse lookup, nearest.</summary>
    public const string Search = "location:search";

    /// <summary>Run hyperlocal queries: nearby, radius, bounding box, polygon, distance.</summary>
    public const string Hyperlocal = "location:hyperlocal";

    /// <summary>Read your own location profile, saved locations and preferences.</summary>
    public const string ReadProfile = "location:profile:read";

    /// <summary>Change your own location profile, saved locations and preferences.</summary>
    public const string ManageProfile = "location:profile:manage";

    /// <summary>Read your own country experience and expertise.</summary>
    public const string ReadExperience = "location:experience:read";

    /// <summary>Declare or amend your own country experience and expertise.</summary>
    public const string ManageExperience = "location:experience:manage";

    /// <summary>Read another member's public country experience.</summary>
    public const string ReadPublic = "location:read-public";

    /// <summary>Create or amend catalogue entries. Staff only.</summary>
    public const string ManageCatalog = "location:catalog:manage";
}
