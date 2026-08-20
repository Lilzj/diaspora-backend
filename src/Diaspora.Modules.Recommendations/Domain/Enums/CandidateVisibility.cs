namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// Who a candidate may be shown to, as its owning module declared it.
/// <para>
/// <b>Projected from the publishing contract, never decided here.</b> This module does not get to
/// have an opinion about whether a housing listing is members-only; it records what Housing said and
/// enforces it. The distinction matters because the enforcement is the same code for every vertical
/// while the decision is eleven different domain rules.
/// </para>
/// </summary>
public enum CandidateVisibility
{
    /// <summary>Anybody, including a signed-out visitor.</summary>
    Public = 0,

    /// <summary>Any signed-in member.</summary>
    Members = 1,

    /// <summary>Only members the Trust Engine has verified.</summary>
    Verified = 2,

    /// <summary>Only members in the candidate's own country.</summary>
    CountryOnly = 3
}
