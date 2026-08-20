using Diaspora.Application.Abstractions;
using Diaspora.Modules.Recommendations.Contracts.V1.Requests;
using Diaspora.Modules.Recommendations.Contracts.V1.Responses;

namespace Diaspora.Modules.Recommendations.Application.Preferences;

/// <summary>
/// Changes what the caller has asked for.
/// <para>
/// <b>Audited, and this is the audit that matters most in the module.</b> A member turning
/// personalization off is a privacy decision, and being able to answer "when did they turn it off,
/// and did we honour it from that moment" is the difference between a claim and a record.
/// </para>
/// <para>
/// Every field is optional and null means "leave it alone" — see
/// <c>UpdatePersonalizationPreferencesRequest</c> for why a request shape where omission meant
/// "clear" would be a defect waiting to happen.
/// </para>
/// </summary>
[Authorize(Permission = RecommendationPermissions.PreferencesManage)]
[Transactional(Reason = "A preference change is one decision; a partial one is a setting nobody chose.")]
[Auditable(Action = "recommendations.preferences.update")]
public sealed record UpdatePersonalizationPreferencesCommand
    : ICommand<PersonalizationPreferencesResponse>
{
    public bool? IsPersonalizationEnabled { get; init; }

    public bool? ShareActivitySignals { get; init; }

    public string? PreferredLanguage { get; init; }

    public string? PreferredCountryCode { get; init; }

    public IReadOnlyList<CategoryPreferenceRequest>? Categories { get; init; }

    public IReadOnlyList<SurfacePreferenceRequest>? Surfaces { get; init; }

    public IReadOnlyList<FavoriteLocationRequest>? FavoriteLocations { get; init; }
}
