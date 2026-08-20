namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>Who may reach a listing, independently of where it is in its lifecycle.</summary>
public enum ListingVisibility
{
    /// <summary>Discoverable by search, browse, nearby and the category pages.</summary>
    Public = 0,

    /// <summary>Reachable by link only. Never in a result set.</summary>
    Unlisted = 1,

    /// <summary>The seller's own eyes. What a draft is, said explicitly.</summary>
    Private = 2
}
