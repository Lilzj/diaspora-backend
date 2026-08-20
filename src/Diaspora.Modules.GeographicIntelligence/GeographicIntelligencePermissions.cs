namespace Diaspora.Modules.GeographicIntelligence;

/// <summary>
/// The permissions the Geographic Intelligence Platform owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use dots —
/// see <see cref="GeographicIntelligencePolicies"/>).
/// </para>
/// <para>
/// <b>Three, and the split is by what a caller learns rather than by which screen asks.</b> A
/// score, a cost band and a density cell are all statements about a <em>place</em>, so they share
/// one permission. A route is a statement about a <em>journey</em>, and a journey has an origin —
/// which for a member routing from where they stand is a precise position. Separating them means a
/// deployment can serve neighbourhood intelligence without accepting precise origins, which is the
/// configuration most deployments should start in.
/// </para>
/// </summary>
public static class GeographicIntelligencePermissions
{
    private const string Prefix = "geo";

    /// <summary>
    /// Read composed geographic intelligence: neighbourhood scores, cost of living, safety and
    /// the density layer.
    /// <para>
    /// <see cref="Diaspora.Contracts.Authorization.AccessTier.Limited"/>, matching B4's hyperlocal
    /// permission. These surfaces are how somebody decides where to live, and a member still
    /// finishing verification is exactly the member deciding.
    /// </para>
    /// </summary>
    public const string IntelligenceRead = Prefix + ":intelligence:read";

    /// <summary>
    /// Ask for a route between two points.
    /// <para>
    /// Its own permission because its input is different in kind: a route request carries an
    /// origin, and an origin is the most sensitive coordinate this platform accepts.
    /// </para>
    /// </summary>
    public const string RoutingRead = Prefix + ":routing:read";

    /// <summary>
    /// Read what this deployment's geographic providers can actually do.
    /// <para>
    /// A client must be able to find out that routing is unavailable <em>before</em> it offers a
    /// Directions button, because guessing is what produces a button that fails.
    /// </para>
    /// <para>
    /// <b><see cref="Diaspora.Contracts.Authorization.AccessTier.Limited"/>, not
    /// <see cref="Diaspora.Contracts.Authorization.AccessTier.Onboarding"/>.</b> Onboarding was the
    /// first instinct and it is wrong: everything this report describes is Limited, so a member who
    /// could read the report a tier earlier still could not reach a single surface it describes.
    /// B2.0.1's onboarding allow-list is deliberately minimal and every entry on it removes a loop
    /// with no entrance. This would have removed none.
    /// </para>
    /// </summary>
    public const string CapabilitiesRead = Prefix + ":capabilities:read";
}
