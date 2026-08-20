using Diaspora.Contracts.Authorization;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Preferences;
using Diaspora.Modules.Recommendations.Domain.Profiles;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Recommendations.Extensibility.Personalization;

/// <summary>
/// Everything known about the member, assembled once and handed to everything.
/// <para>
/// <b>One read, one object, passed by reference to every strategy and every policy — and that is a
/// performance decision that turned into a correctness one.</b> A generation runs ten strategies and
/// nine policies over four hundred candidates. If each resolved the profile itself, that is nineteen
/// reads of the same row per request; worse, two of them could see different values if a concurrent
/// fold committed between them, and the page would be ranked against one profile and explained
/// against another.
/// </para>
/// <para>
/// <b>Nothing here is nullable except the two things that genuinely may not exist.</b> A profile is
/// created on first sight so it is always present, even if empty; a preference row exists only once
/// a member has stated something, and its absence means "every default" rather than "unknown".
/// </para>
/// <para>
/// <b><see cref="AsOf"/> is carried rather than read from a clock.</b> Every decay, expiry and
/// freshness comparison in a generation must agree on what "now" is, or a candidate can be fresh to
/// the recency strategy and expired to the availability policy in the same pass.
/// </para>
/// </summary>
public sealed record PersonalizationContext
{
    public required UserId UserId { get; init; }

    /// <summary>Always present. Empty for a member the platform has never observed.</summary>
    public required PersonalizationProfile Profile { get; init; }

    /// <summary>Null means the member has stated nothing. See the class summary.</summary>
    public PersonalizationPreference? Preference { get; init; }

    public required RecommendationSurface Surface { get; init; }

    /// <summary>Set when the surface asked for one kind of thing. Null on the home feed.</summary>
    public RecommendationTargetType? TargetType { get; init; }

    /// <summary>The single moment the whole generation is evaluated against. See the class summary.</summary>
    public required DateTimeOffset AsOf { get; init; }

    /// <summary>Whether the cold-start path is running. Decided once, by the engine.</summary>
    public required bool IsColdStart { get; init; }

    /// <summary>
    /// The member's access tier, as the caller's claims reported it.
    /// <para>
    /// Passed in rather than resolved here because this module must not reference Identity, and
    /// because the tier is a property of the request rather than of the member — a service token
    /// generating a digest has no tier at all.
    /// </para>
    /// </summary>
    public required AccessTier AccessTier { get; init; }

    /// <summary>
    /// The country the member should be recommended for.
    /// <para>
    /// Their stated preference when they have one, their actual country otherwise. Resolved once
    /// here so the intended-mover case does not have to be re-derived in every strategy.
    /// </para>
    /// </summary>
    public string? EffectiveCountryCode =>
        Preference?.PreferredCountryCode ?? Profile.PrimaryCountryCode;

    /// <summary>Their stated language when they have one, their profile's otherwise.</summary>
    public string? EffectiveLanguage =>
        Preference?.PreferredLanguage ?? Profile.PreferredLanguage;

    /// <summary>
    /// Whether the member's behaviour may be used at all.
    /// <para>
    /// False makes every ranking pass behave as though the profile were empty, which is the only
    /// honest implementation of a personalization opt-out. Checked by the engine, and again by the
    /// privacy policy, because a strategy registered by a future adapter will not have read this
    /// paragraph.
    /// </para>
    /// </summary>
    public bool IsPersonalizationAllowed =>
        Preference?.IsPersonalizationEnabled ?? true;

    /// <summary>Whether this surface in particular is personalised.</summary>
    public bool IsSurfacePersonalized =>
        IsPersonalizationAllowed && (Preference?.IsSurfaceEnabled(Surface) ?? true);
}
