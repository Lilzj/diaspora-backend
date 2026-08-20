namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// How widely a policy version applies.
/// <para>
/// Resolution is most-specific-first: a product override beats a country override, which beats the
/// platform default. A policy with no applicable version is a refusal, never a guessed zero — see
/// <c>IFinancialPolicyEngine</c>.
/// </para>
/// </summary>
public enum PolicyScopeKind
{
    /// <summary>Everywhere, unless something narrower matches.</summary>
    Platform = 1,

    /// <summary>One ISO 3166-1 alpha-2 country.</summary>
    Country = 2,

    /// <summary>One module — marketplace, housing, jobs.</summary>
    Module = 3,

    /// <summary>One monetization product code.</summary>
    Product = 4
}
