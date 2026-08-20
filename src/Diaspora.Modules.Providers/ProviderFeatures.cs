namespace Diaspora.Modules.Providers;

/// <summary>
/// The feature gates the Providers module owns.
/// <para>
/// Gates exist so a client can grey out a control with a reason instead of offering it and
/// failing. The client already has the screen for this — <c>FeatureGate</c> renders a lock card
/// naming the feature, and <c>discover.tsx</c> wraps its results list in
/// <c>&lt;FeatureGate featureName={activeTab}&gt;</c>, which passes the literal string
/// <c>"Providers"</c>. <see cref="Discovery"/> is that gate.
/// </para>
/// </summary>
public static class ProviderFeatures
{
    /// <summary>The client's <c>FeatureGate featureName="Providers"</c> on the discover tab.</summary>
    public const string Discovery = "providers.discovery";

    /// <summary>The client's "Create Provider Listing" card on the post and create screens.</summary>
    public const string Registration = "providers.registration";

    /// <summary>Editing a provider the caller owns.</summary>
    public const string Management = "providers.management";

    /// <summary>Inviting managers, moderators and editors.</summary>
    public const string Staffing = "providers.staffing";

    /// <summary>Publishing services and prices.</summary>
    public const string ServiceCatalog = "providers.service-catalog";

    /// <summary>Setting operating hours and availability exceptions.</summary>
    public const string Availability = "providers.availability";

    /// <summary>Submitting a provider for business or organization verification.</summary>
    public const string Verification = "providers.verification";

    /// <summary>Writing a review.</summary>
    public const string Reviews = "providers.reviews";


    /// <summary>Recommending or endorsing.</summary>
    public const string Recommendations = "providers.recommendations";

    /// <summary>The owner-facing statistics panel.</summary>
    public const string Statistics = "providers.statistics";

    /// <summary>Being eligible to appear in the featured carousel.</summary>
    public const string FeaturedPlacement = "providers.featured-placement";
}
