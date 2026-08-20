namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// A condition a buyer must satisfy before a product is offered.
/// <para>
/// Evaluated against facts this module already holds or is told in the request, never by asking
/// another module. <see cref="MinimumAccessTier"/> and <see cref="VerifiedOnly"/> read the caller's
/// own claims; <see cref="CountryAllowList"/> reads the request. Nothing here queries a vertical.
/// </para>
/// </summary>
public enum EligibilityRuleKind
{
    MinimumAccessTier = 1,
    VerifiedOnly = 2,
    CountryAllowList = 3,
    CountryDenyList = 4,
    SubscriptionRequired = 5,

    /// <summary>Buyable once per subject, ever.</summary>
    OneTimeOnly = 6,

    /// <summary>Whatever a registered rule implementation decides.</summary>
    Custom = 99
}
