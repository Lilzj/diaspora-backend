namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Whose money a financial identity holds.
/// <para>
/// Answers "whose", where <see cref="WalletType"/> answers "which pot". The brief lists personal,
/// organization, provider and future business wallets; those are four owners rather than four kinds
/// of container, and separating the two questions is what lets an organization hold a primary
/// wallet and an escrow wallet without the type meaning two things at once.
/// </para>
/// </summary>
public enum FinancialAccountType
{
    /// <summary>An individual member.</summary>
    Personal = 1,

    /// <summary>A community organization. B12 owns the organization; this owns its money.</summary>
    Organization = 2,

    /// <summary>A service provider. B7 owns the provider; this owns its money.</summary>
    Provider = 3,

    /// <summary>Reserved for business accounts. No provisioning path today.</summary>
    Business = 4,

    /// <summary>The platform itself. Commission, fees and revenue land here.</summary>
    Platform = 5
}
