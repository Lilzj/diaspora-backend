namespace Diaspora.Contracts.Moderation.V1;

/// <summary>
/// Why staff act on somebody's content: one list for every module (7 Nov 2026).
/// <para>
/// Each module keeps its own report reasons for what members choose from, and maps them onto these.
/// A decision always carries one of these plus a written note; the owner is told both. Stored and
/// sent by name, so a code can be added without renumbering anything.
/// </para>
/// </summary>
public enum ModerationReasonCode
{
    FalseOrMisleading,
    FraudOrScam,
    AsksForFees,
    ProhibitedItem,
    OffensiveOrHarassment,
    Discriminatory,
    Spam,
    Duplicate,
    Impersonation,
    ChildSafety,
    ViolenceOrThreats,
    Copyright,
    PersonalData,
    Other
}

/// <summary>What each reason means, and which carry the owner's special rules.</summary>
public static class ModerationReasons
{
    /// <summary>
    /// The reasons that let one moderator hide something at once, with a second staff member
    /// reviewing it within <see cref="ModerationPolicy.EmergencySecondReview"/>. The owner's
    /// choice (7 Nov 2026): the four recommended plus false or misleading information.
    /// </summary>
    public static bool IsEmergency(ModerationReasonCode code) => code is
        ModerationReasonCode.FalseOrMisleading
        or ModerationReasonCode.ChildSafety
        or ModerationReasonCode.ViolenceOrThreats
        or ModerationReasonCode.AsksForFees
        or ModerationReasonCode.Impersonation;

    /// <summary>A fraud or scam report reaches the queue on its own; anything else needs a second.</summary>
    public static bool QueuesOnFirstReport(ModerationReasonCode code) => code is
        ModerationReasonCode.FraudOrScam or ModerationReasonCode.AsksForFees;

    /// <summary>
    /// The reasons serious enough for many reports to hide something before anyone has looked,
    /// as "hidden pending review", with the owner told at once.
    /// </summary>
    public static bool IsSerious(ModerationReasonCode code) =>
        IsEmergency(code)
        || code is ModerationReasonCode.FraudOrScam
            or ModerationReasonCode.Discriminatory
            or ModerationReasonCode.OffensiveOrHarassment;

    /// <summary>The reason as the owner reads it, completing "it was found to be …".</summary>
    public static string Label(ModerationReasonCode code) => code switch
    {
        ModerationReasonCode.FalseOrMisleading => "false or misleading",
        ModerationReasonCode.FraudOrScam => "a fraud or scam",
        ModerationReasonCode.AsksForFees => "asking people to pay up front",
        ModerationReasonCode.ProhibitedItem => "offering something not allowed on Diaspora",
        ModerationReasonCode.OffensiveOrHarassment => "offensive or harassing",
        ModerationReasonCode.Discriminatory => "discriminatory",
        ModerationReasonCode.Spam => "spam",
        ModerationReasonCode.Duplicate => "a duplicate",
        ModerationReasonCode.Impersonation => "impersonating a person or organisation",
        ModerationReasonCode.ChildSafety => "a risk to a child's safety",
        ModerationReasonCode.ViolenceOrThreats => "violent or threatening",
        ModerationReasonCode.Copyright => "using someone else's work without permission",
        ModerationReasonCode.PersonalData => "sharing someone's personal information",
        _ => "against Diaspora's rules"
    };

    public static bool TryParse(string? value, out ModerationReasonCode code) =>
        Enum.TryParse(value?.Trim(), ignoreCase: true, out code)
        && Enum.IsDefined(code);
}
