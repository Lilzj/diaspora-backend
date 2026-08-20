namespace Diaspora.Modules.Recommendations;

/// <summary>
/// The permissions the Recommendation Platform owns.
/// <para>
/// <b>Two bands, and the line between them is "whose experience does this change".</b> Everything a
/// member does about their own feed — read it, dismiss an item, mute a category, turn
/// personalization off entirely, rebuild their profile — is theirs and is available at the lowest
/// tier the platform has, including to a restricted account. A suspended member still deserves to
/// be told which guide explains how to appeal.
/// </para>
/// <para>
/// Everything that decides what other people are shown — a strategy weight, a policy, the analytics
/// that justify changing either — is staff, because a ranking weight is a single number that moves
/// what an entire country sees.
/// </para>
/// </summary>
public static class RecommendationPermissions
{
    // ── The member's own surface ────────────────────────────────────────────────────────────

    public const string FeedRead = "recommendations:read";
    public const string SignalsRecord = "recommendations.signals:record";
    public const string ProfileRead = "recommendations.profile:read";
    public const string ProfileManage = "recommendations.profile:manage";
    public const string PreferencesRead = "recommendations.preferences:read";
    public const string PreferencesManage = "recommendations.preferences:manage";
    public const string FeedbackRecord = "recommendations.feedback:record";

    // ── What everybody else sees ────────────────────────────────────────────────────────────

    public const string StrategiesRead = "recommendations.strategies:read";
    public const string StrategiesManage = "recommendations.strategies:manage";
    public const string PoliciesRead = "recommendations.policies:read";
    public const string PoliciesManage = "recommendations.policies:manage";
    public const string AnalyticsRead = "recommendations.analytics:read";
    public const string CandidatesRead = "recommendations.candidates:read";
    public const string OperationsRun = "recommendations.operations:run";
}
