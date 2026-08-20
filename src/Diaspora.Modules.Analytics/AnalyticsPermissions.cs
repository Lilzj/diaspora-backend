namespace Diaspora.Modules.Analytics;

/// <summary>
/// The permissions the Analytics Platform owns.
/// <para>
/// Colon-separated keys with the module prefix, per the platform convention (policies use dots —
/// see <see cref="AnalyticsPolicies"/>). Tier floors and role sets live with the definitions in
/// <c>Authorization/AnalyticsContributors</c>.
/// </para>
/// <para>
/// <b>A permission is a necessary condition and never a sufficient one, and analytics is the
/// surface where that matters most.</b> Holding <see cref="ContentReadOwn"/> says a caller may
/// read content performance; it says nothing about <em>whose</em>. Every read in this module
/// resolves the subject's owner from the analytics dimension and compares it to the caller on the
/// request. The register calls the alternative out by name: an owner-scoped route whose id is not
/// checked against the caller is "the single most likely place for an IDOR in the phase".
/// </para>
/// </summary>
public static class AnalyticsPermissions
{
    private const string Prefix = "analytics";

    // ── Member-scoped reads ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Read the caller's own creator analytics.
    /// <para>
    /// Self only. There is deliberately no permission that names another creator, because a
    /// contract that cannot address one cannot leak one — which is stronger than a check.
    /// </para>
    /// </summary>
    public const string CreatorRead = Prefix + ":creator:read";

    /// <summary>
    /// Read performance for content the caller owns.
    /// <para>
    /// Four segments because the scope is what carries the authority: this is not "read content
    /// analytics", it is "read your own". The platform's precedent is
    /// <c>community-life:event:edit:own</c>.
    /// </para>
    /// </summary>
    public const string ContentReadOwn = Prefix + ":content:read:own";

    // ── Ingestion ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Submit client telemetry.
    /// <para>
    /// A permission rather than "any authenticated caller" so a deployment can revoke ingestion
    /// from a restricted account without turning the endpoint off for everybody, and so the
    /// capability appears in the platform catalogue like every other.
    /// </para>
    /// </summary>
    public const string TelemetryWrite = Prefix + ":telemetry:write";

    // ── Privileged ──────────────────────────────────────────────────────────────────────────

    /// <summary>Read analytics for a subject the caller does not own. Administrative.</summary>
    public const string PlatformRead = Prefix + ":platform:read";

    /// <summary>Run a bounded backfill or rebuild of the analytics rollups.</summary>
    public const string OperationsRun = Prefix + ":operations:run";
}
