namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// Everything the caller has asked for.
/// <para>
/// <b>Returned in full even when the member has stated nothing.</b> A response of defaults is what
/// lets the settings screen render without a special case, and a client that had to distinguish "no
/// preferences row" from "preferences at their defaults" would get it wrong in one of the two.
/// </para>
/// </summary>
public sealed record PersonalizationPreferencesResponse
{
    /// <summary>The master switch. False means the profile is not consulted at all.</summary>
    public required bool IsPersonalizationEnabled { get; init; }

    /// <summary>Narrower: whether behavioural signals may be recorded.</summary>
    public required bool ShareActivitySignals { get; init; }

    public string? PreferredLanguage { get; init; }

    /// <summary>The intended-mover case. Overrides where the member actually is.</summary>
    public string? PreferredCountryCode { get; init; }

    public required IReadOnlyList<CategoryPreferenceResponse> Categories { get; init; }

    public required IReadOnlyList<BlockedTargetResponse> BlockedTargets { get; init; }

    public required IReadOnlyList<SurfacePreferenceResponse> Surfaces { get; init; }

    public required IReadOnlyList<FavoriteLocationResponse> FavoriteLocations { get; init; }
}
