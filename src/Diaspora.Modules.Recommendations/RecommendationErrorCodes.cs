namespace Diaspora.Modules.Recommendations;

/// <summary>
/// The error codes this module returns, in one place.
/// <para>
/// Declared once rather than inline because a code is a contract the mobile client switches on, and
/// a code spelled two ways in two handlers is a client branch that silently stops matching.
/// </para>
/// </summary>
public static class RecommendationErrorCodes
{
    public const string NotAuthenticated = "recommendations.not_authenticated";
    public const string Forbidden = "recommendations.forbidden";

    public const string SignalInvalid = "recommendations.signal.invalid";
    public const string SignalKindUnknown = "recommendations.signal.kind_unknown";
    public const string SignalBatchTooLarge = "recommendations.signal.batch_too_large";

    public const string ProfileNotFound = "recommendations.profile.not_found";
    public const string ProfileRebuildThrottled = "recommendations.profile.rebuild_throttled";

    public const string PreferenceInvalid = "recommendations.preference.invalid";

    public const string SurfaceUnknown = "recommendations.surface.unknown";
    public const string TargetTypeUnknown = "recommendations.target_type.unknown";
    public const string TargetTypeNotOnSurface = "recommendations.target_type.not_on_surface";

    public const string RecommendationNotFound = "recommendations.item.not_found";
    public const string RecommendationSetNotFound = "recommendations.set.not_found";

    public const string StrategyNotFound = "recommendations.strategy.not_found";
    public const string StrategyDuplicate = "recommendations.strategy.duplicate";
    public const string StrategyWeightInvalid = "recommendations.strategy.weight_invalid";
    public const string StrategyUnimplemented = "recommendations.strategy.unimplemented";

    public const string PolicyNotFound = "recommendations.policy.not_found";
    public const string PolicyDuplicate = "recommendations.policy.duplicate";
    public const string PolicyImmutable = "recommendations.policy.immutable";

    public const string PersonalizationDisabled = "recommendations.personalization.disabled";

    public const string ConcurrencyConflict = "recommendations.concurrency_conflict";
}
