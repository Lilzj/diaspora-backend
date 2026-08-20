using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Trust.Observability;

/// <summary>
/// The module's live telemetry instruments.
/// <para>
/// A module that declares an activity source through <c>IObservabilityContributor</c> must
/// also create one, or it subscribes the tracer to silence — the B2 mistake B2.6 exists to
/// prevent. These are real and the handlers emit through them.
/// </para>
/// <para>
/// The counters are chosen to answer the questions an on-call engineer actually asks about a
/// trust engine: is the review queue draining, are we auto-rejecting more than usual, and are
/// appeals being upheld at a rate that suggests the original decisions are wrong.
/// </para>
/// </summary>
public static class TrustTelemetrySource
{
    public static readonly ActivitySource Activity = new(TrustTelemetry.SourceName, "1.0.0");

    public static readonly Meter Meter = new(TrustTelemetry.SourceName, "1.0.0");

    public static readonly Counter<long> VerificationsSubmitted =
        Meter.CreateCounter<long>(
            "diaspora.trust.verifications.submitted",
            unit: "{verification}",
            description: "Verification requests submitted, by type.");

    // ── B3.0.2, the in-process submission contract ────────────────────────
    // Separate from VerificationsSubmitted, which counts cases opened by any route. These
    // count the contract's own traffic, and the pair a reviewer of this integration wants is
    // started against accepted: a gap between them is submissions being refused, and the
    // rejected counter's reason dimension says why.

    public static readonly Counter<long> SubmissionsStarted =
        Meter.CreateCounter<long>(
            "diaspora.trust.submissions.started",
            unit: "{submission}",
            description: "In-process submissions received, by type, subject and submitting module.");

    public static readonly Counter<long> SubmissionsAccepted =
        Meter.CreateCounter<long>(
            "diaspora.trust.submissions.accepted",
            unit: "{submission}",
            description: "In-process submissions that opened a new verification case.");

    public static readonly Counter<long> SubmissionsRejected =
        Meter.CreateCounter<long>(
            "diaspora.trust.submissions.rejected",
            unit: "{submission}",
            description: "In-process submissions refused, by reason and submitting module.");

    public static readonly Counter<long> SubmissionsDuplicate =
        Meter.CreateCounter<long>(
            "diaspora.trust.submissions.duplicate",
            unit: "{submission}",
            description: "In-process submissions that matched a case already held. A retry "
                + "working as intended, not an error — but a sustained rise means a caller is "
                + "retrying something that is not completing.");

    public static readonly Counter<long> VerificationsDecided =
        Meter.CreateCounter<long>(
            "diaspora.trust.verifications.decided",
            unit: "{verification}",
            description: "Verification decisions, by type, outcome and whether a human decided.");

    public static readonly Counter<long> AppealsSubmitted =
        Meter.CreateCounter<long>(
            "diaspora.trust.appeals.submitted",
            unit: "{appeal}",
            description: "Appeals filed, by type.");

    public static readonly Counter<long> AppealsDecided =
        Meter.CreateCounter<long>(
            "diaspora.trust.appeals.decided",
            unit: "{appeal}",
            description: "Appeal outcomes, by type and whether upheld. A rising upheld rate "
                + "means the original decisions need attention.");

    public static readonly Counter<long> TrustLevelChanges =
        Meter.CreateCounter<long>(
            "diaspora.trust.level.changed",
            unit: "{member}",
            description: "Trust tier movements, by direction.");

    public static readonly Counter<long> BadgesAwarded =
        Meter.CreateCounter<long>(
            "diaspora.trust.badges.awarded",
            unit: "{badge}",
            description: "Badges awarded, by badge type.");

    public static readonly Counter<long> EligibilityChecks =
        Meter.CreateCounter<long>(
            "diaspora.trust.eligibility.checked",
            unit: "{check}",
            description: "Eligibility evaluations, by capability and outcome code.");

    public static readonly Counter<long> ModerationDecisions =
        Meter.CreateCounter<long>(
            "diaspora.trust.moderation.decided",
            unit: "{decision}",
            description: "Moderation decisions, by content type and action.");

    public static readonly Histogram<double> ReviewLatencySeconds =
        Meter.CreateHistogram<double>(
            "diaspora.trust.verifications.review_latency",
            unit: "s",
            description: "Time from submission to decision. The number the review SLA is measured against.");
}
