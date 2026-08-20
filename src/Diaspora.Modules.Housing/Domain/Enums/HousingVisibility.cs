namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>Who may reach a listing, independent of where it is in its lifecycle.</summary>
public enum HousingVisibility
{
    /// <summary>Discoverable by anybody, including signed-out visitors.</summary>
    Public = 0,

    /// <summary>Reachable by direct link only. Never appears in search or a feed.</summary>
    Unlisted = 1,

    /// <summary>The landlord and staff only. What a draft is, and what a withdrawn listing becomes.</summary>
    Private = 2
}
