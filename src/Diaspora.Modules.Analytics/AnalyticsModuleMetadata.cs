namespace Diaspora.Modules.Analytics;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class AnalyticsModuleMetadata
{
    public const string Name = "Analytics";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "analytics";

    // ── The module's public URL space ────────────────────────────────────────────────────────
    //
    // Two prefixes, and the split is a trust boundary rather than a filing convenience.
    //
    // `/api/v1/analytics` is read-only to a member and every route under it is authorised against
    // a subject the caller must already be entitled to see. `/api/v1/telemetry` is the one write
    // path, open to any authenticated caller, rate-limited, and treated as untrusted input from
    // the first line of the handler. Putting the write beside the reads would have made "is this
    // route a read" a question about the path rather than about the prefix.
    //
    // Neither is `/api/v1/creators`. The Phase 7 register writes the creator analytics routes as
    // `GET /api/v1/creators/me/analytics`, which is B18's URL space, owned by B18, and asserted as
    // B18's by an architecture test. The Phase 15 register — written later, over the same
    // capability — puts them at `/analytics/creators/me`, which is this module's, and that is what
    // ships. See the Frontend Contract Reconciliation section of the Wave 7 report for the exact
    // client change: one path per call site, no request or response shape altered.

    /// <summary>
    /// Every analytics read: access, definitions, the creator dataset, content performance and the
    /// administrative operations.
    /// </summary>
    public const string AnalyticsRoutePrefix = "/api/v1/analytics";

    /// <summary>
    /// Client telemetry ingestion — FRONTEND-PHASE15-GAP-029, restating CREATOR-GAP-016.
    /// <para>
    /// A separate space because it is the only write, and because the client's own instrumentation
    /// module has named this destination since Phase 7: "when <c>POST /api/v1/telemetry/events</c>
    /// exists, this function batches from the buffer and posts. Nothing else in the app changes."
    /// </para>
    /// </summary>
    public const string TelemetryRoutePrefix = "/api/v1/telemetry";
}
