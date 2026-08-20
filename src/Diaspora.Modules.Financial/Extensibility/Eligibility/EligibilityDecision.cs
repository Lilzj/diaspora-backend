namespace Diaspora.Modules.Financial.Extensibility.Eligibility;

/// <summary>
/// Whether a product may be bought, and if not, why.
/// <para>
/// <b>The reason is required on a refusal.</b> A product that silently vanishes from the catalogue
/// leaves the member with no idea what to do; one that appears greyed out with "verify your phone
/// number to buy this" is a conversion path. The client renders <see cref="Reason"/>, so it has to
/// be there.
/// </para>
/// </summary>
public sealed record EligibilityDecision
{
    public required bool IsEligible { get; init; }

    /// <summary>A code the client can translate. Null only when eligible.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>A sentence a member can act on. Null only when eligible.</summary>
    public string? Reason { get; init; }

    public static EligibilityDecision Eligible() => new() { IsEligible = true };

    public static EligibilityDecision Refused(string reasonCode, string reason) =>
        new() { IsEligible = false, ReasonCode = reasonCode, Reason = reason };
}
