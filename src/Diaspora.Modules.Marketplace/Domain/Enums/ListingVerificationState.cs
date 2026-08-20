namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// A projection of what Trust decided. Nothing in this module may set it to
/// <see cref="Verified"/> except a handler for a Trust integration event.
/// </summary>
public enum ListingVerificationState
{
    /// <summary>Never claimed.</summary>
    None = 0,

    /// <summary>Claimed, and with Trust.</summary>
    Pending = 1,

    /// <summary>Trust confirmed it.</summary>
    Verified = 2,

    /// <summary>Trust refused the claim.</summary>
    Rejected = 3,

    /// <summary>Trust withdrew a confirmation it had made.</summary>
    Revoked = 4
}
