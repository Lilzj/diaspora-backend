namespace Diaspora.Modules.Analytics.Contracts.V1.Responses;

/// <summary>
/// What the <b>server</b> says this caller may see for this subject.
/// <para>
/// FRONTEND-PHASE15-GAP-025, and the register calls it the linchpin: every other contract's
/// authorization line depends on this one being real. §48 forbids deriving a capability from a role
/// held on the device, and the client contains no code path that does — which only helps if the
/// server fills every field in.
/// </para>
/// <para>
/// <b>A caller with no access gets an all-false set rather than a 403.</b> Two reasons, and the
/// second is the one that matters: a screen can render "not available to you" instead of an error,
/// and the existence of an administrative dashboard is not confirmed by a status code.
/// </para>
/// </summary>
public sealed record AnalyticsAccessResponse
{
    public required AnalyticsSubjectResponse Subject { get; init; }

    public required bool CanView { get; init; }

    /// <summary>Money. Separately gated, and false everywhere on this deployment — see the report.</summary>
    public required bool CanViewFinancial { get; init; }

    public required bool CanViewAdministrative { get; init; }

    /// <summary>Whether this caller may compare this subject against others.</summary>
    public required bool CanCompare { get; init; }

    public required bool CanExport { get; init; }

    public required bool CanBuildReports { get; init; }

    /// <summary>
    /// Which ranges this deployment will actually answer for this subject.
    /// <para>
    /// A capability rather than a fact about the client's union, which is what lets a deployment
    /// that gains longer retention offer more ranges with no client release. §6 asks for exactly
    /// this.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> SupportedRanges { get; init; }

    public required IReadOnlyList<string> SupportedComparisons { get; init; }

    /// <summary>
    /// Metric keys withheld from this caller, so the client omits rather than nulls.
    /// <para>
    /// Distinct from a metric this deployment does not measure: one is about the caller, the other
    /// about the platform. The dataset's <c>unavailableMetrics</c> carries the second.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> RestrictedMetrics { get; init; }

    /// <summary>The IANA zone every day boundary on this deployment is cut in.</summary>
    public required string ReportingTimezone { get; init; }
}

/// <summary>
/// One entry in the metric catalogue.
/// <para>
/// FRONTEND-PHASE15-GAP-030. The client ships a catalogue today, which is the right trade — a
/// definition it cannot show because the network is down is worse than one a release behind — and
/// the gap is that the backend had none of its own to disagree with. It does now, it is the one the
/// aggregation actually uses, and every metric carries its sentence inline as well.
/// </para>
/// </summary>
public sealed record AnalyticsDefinitionResponse
{
    /// <summary>The Phase 7 key the client switches on.</summary>
    public required string Key { get; init; }

    /// <summary>The Phase 15 dotted catalogue key for the same metric.</summary>
    public required string CatalogueKey { get; init; }

    public required string Label { get; init; }

    /// <summary>One sentence. What the number counts.</summary>
    public required string Definition { get; init; }

    /// <summary>How it is calculated, where a formula is meaningful.</summary>
    public string? Formula { get; init; }

    public required string Format { get; init; }

    public required bool HigherIsBetter { get; init; }

    public required string PrivacyClass { get; init; }

    /// <summary>Which backend module owns the truth behind it.</summary>
    public required string Owner { get; init; }

    /// <summary>How often it is recomputed, in the platform's words.</summary>
    public required string FreshnessPolicy { get; init; }

    /// <summary>
    /// Which classes of fact contribute: <c>server</c>, <c>client</c>, or <c>derived</c>.
    /// <para>
    /// Published so a reader can tell a figure the platform committed from one a device reported.
    /// §41's distinction, made visible rather than only enforced.
    /// </para>
    /// </summary>
    public required string TrustClass { get; init; }

    /// <summary><c>CONNECTED</c>, <c>AWAITING_SOURCE</c> or <c>NOT_MEASURED</c>.</summary>
    public required string Status { get; init; }
}

/// <summary>The catalogue, with the version the figures were computed under.</summary>
public sealed record AnalyticsDefinitionsResponse
{
    public required string Version { get; init; }

    public required IReadOnlyList<AnalyticsDefinitionResponse> Definitions { get; init; }
}

/// <summary>
/// What happened to one submitted telemetry event.
/// <para>
/// <b>Per event, not per batch, and that is FRONTEND-PHASE15-GAP-029's requirement.</b> A batch that
/// answered "202, thanks" would leave a client unable to tell an accepted event from one the schema
/// registry has never heard of — which is exactly the silent drop the registry exists to prevent.
/// </para>
/// </summary>
public sealed record TelemetryEventResultResponse
{
    /// <summary>The submitted event's position in the batch, so a client can match it back.</summary>
    public required int Index { get; init; }

    public required string Event { get; init; }

    /// <summary><c>accepted</c>, <c>duplicate</c> or <c>rejected</c>.</summary>
    public required string Status { get; init; }

    /// <summary>The refusal code when rejected. One of <c>AnalyticsErrorCodes</c>' ingest codes.</summary>
    public string? Reason { get; init; }
}

/// <summary>The answer to a telemetry batch.</summary>
public sealed record TelemetryIngestionResponse
{
    public required int Accepted { get; init; }

    public required int Duplicate { get; init; }

    public required int Rejected { get; init; }

    public required IReadOnlyList<TelemetryEventResultResponse> Results { get; init; }
}

/// <summary>An administrative backfill run.</summary>
public sealed record AnalyticsBackfillResponse
{
    public required string RunId { get; init; }

    public required string Status { get; init; }

    public required string FromDay { get; init; }

    public required string ToDay { get; init; }

    /// <summary>The day the next slice starts at. What makes a killed run resumable.</summary>
    public required string NextDay { get; init; }

    public required long CellsRebuilt { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    public string? FailureReason { get; init; }
}
