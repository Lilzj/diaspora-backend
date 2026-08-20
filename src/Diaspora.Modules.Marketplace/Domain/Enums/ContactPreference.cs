namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// How the seller wants to be reached about a listing.
/// <para>
/// <b>A preference, and never a channel this module operates.</b> Marketplace owns no chat and
/// sends no mail; what it owns is the seller's stated wish, which the client renders and a future
/// Communication module reads. <see cref="InApp"/> is the default because it is the only option
/// that does not publish a personal contact detail to every stranger who opens the listing.
/// </para>
/// </summary>
public enum ContactPreference
{
    /// <summary>Through the platform. The default, and the only one that leaks nothing.</summary>
    InApp = 0,

    Phone = 1,

    Email = 2,

    /// <summary>Any of the above.</summary>
    Any = 3
}
