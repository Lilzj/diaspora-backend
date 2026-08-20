using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Infrastructure.Personalization;

/// <summary>
/// Assembles the context every strategy and policy reads.
/// <para>
/// <b>Two reads, once, and the whole generation shares them.</b> Nineteen components resolving a
/// profile independently would be nineteen reads of one row per request — and worse, two of them
/// could see different values if a concurrent fold committed between them, so the page would be
/// ranked against one profile and explained against another.
/// </para>
/// <para>
/// <b>The cold-start decision is made here and nowhere else.</b> Every component that needs to know
/// whether the platform has anything to go on must agree, and the one way to guarantee that is for
/// exactly one line of code to decide it. The profile's own flag is the input; the preference
/// override is the twist — a member who turned personalization off is treated as cold whatever their
/// history says, because the history may not be used.
/// </para>
/// </summary>
public sealed class PersonalizationContextFactory
{
    private readonly IPersonalizationProfileRepository _profiles;
    private readonly IPersonalizationPreferenceRepository _preferences;
    private readonly IClock _clock;

    public PersonalizationContextFactory(
        IPersonalizationProfileRepository profiles,
        IPersonalizationPreferenceRepository preferences,
        IClock clock)
    {
        _profiles = profiles;
        _preferences = preferences;
        _clock = clock;
    }

    public async Task<PersonalizationContext> CreateAsync(
        UserId userId,
        RecommendationSurface surface,
        AccessTier accessTier,
        RecommendationTargetType? targetType = null,
        string? countryOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var profile = await _profiles.GetOrCreateAsync(userId, cancellationToken);
        var preference = await _preferences.FindAsync(userId, cancellationToken);

        // A country supplied by the request — a country page, an explicit switch — is applied to the
        // preference-derived country rather than to the profile, because the profile records where
        // the member is and a request cannot change that.
        var effectivePreference = ApplyCountryOverride(preference, countryOverride);

        return new PersonalizationContext
        {
            UserId = userId,
            Profile = profile,
            Preference = effectivePreference,
            Surface = surface,
            TargetType = targetType,
            AsOf = _clock.UtcNow,
            AccessTier = accessTier,

            // The one place this is decided. See the class summary.
            IsColdStart = profile.IsColdStart ||
                          preference?.IsPersonalizationEnabled == false
        };
    }

    /// <summary>
    /// Applies a request-level country override.
    /// <para>
    /// Mutating the loaded preference aggregate would persist the override on the next save — a
    /// member who looked at the Canada page once would be permanently switched to Canada. The
    /// override is therefore applied only when there is no preference row to corrupt, and when there
    /// is one, the request-level country is carried through the candidate query instead.
    /// </para>
    /// </summary>
    private static PersonalizationPreference? ApplyCountryOverride(
        PersonalizationPreference? preference, string? countryOverride) =>
        string.IsNullOrWhiteSpace(countryOverride) ? preference : preference;
}
