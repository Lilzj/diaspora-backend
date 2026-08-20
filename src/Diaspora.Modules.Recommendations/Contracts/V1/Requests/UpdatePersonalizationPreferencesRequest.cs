namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>
/// A change to what the caller has asked for.
/// <para>
/// <b>Every property is nullable and null means "leave it alone", which is what lets one settings
/// screen submit one field at a time.</b> A request shape where omission meant "clear" would make a
/// client that only wanted to change the language silently unmute every category the member had
/// muted — the sort of defect that is discovered by a support ticket rather than a test.
/// </para>
/// </summary>
public sealed record UpdatePersonalizationPreferencesRequest
{
    /// <summary>The master switch. Off means the profile is not consulted at all.</summary>
    public bool? IsPersonalizationEnabled { get; init; }

    /// <summary>Narrower: whether behavioural signals may be recorded.</summary>
    public bool? ShareActivitySignals { get; init; }

    public string? PreferredLanguage { get; init; }

    /// <summary>The intended-mover case. Overrides where the member actually is.</summary>
    public string? PreferredCountryCode { get; init; }

    /// <summary>Categories to mute or prefer. Absent categories are left as they are.</summary>
    public IReadOnlyList<CategoryPreferenceRequest>? Categories { get; init; }

    /// <summary>Surfaces to enable or disable. Absent surfaces are left as they are.</summary>
    public IReadOnlyList<SurfacePreferenceRequest>? Surfaces { get; init; }

    /// <summary>Places to favour. Absent places are left as they are.</summary>
    public IReadOnlyList<FavoriteLocationRequest>? FavoriteLocations { get; init; }
}
