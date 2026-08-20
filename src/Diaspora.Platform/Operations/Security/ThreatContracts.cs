namespace Diaspora.Platform.Operations.Security;

/// <summary>What kind of threat a signal reports.</summary>
public enum ThreatKind
{
    /// <summary>A sign-in that does not look like this member's other sign-ins.</summary>
    SuspiciousLogin = 0,

    /// <summary>Credentials being tried at a rate or breadth that is not a person.</summary>
    CredentialAbuse = 1,

    /// <summary>Traffic whose shape changed sharply.</summary>
    TrafficAnomaly = 2,

    /// <summary>A financial pattern worth a second look.</summary>
    FinancialAnomaly = 3,

    /// <summary>The recommendation engine being gamed.</summary>
    RecommendationAbuse = 4,

    /// <summary>The notification pipeline being used to send things nobody asked for.</summary>
    NotificationAbuse = 5,

    /// <summary>Reserved for B16. AI capabilities being abused.</summary>
    AiAbuse = 6,

    /// <summary>Something that does not fit the others.</summary>
    Other = 99
}

/// <summary>How seriously a signal should be taken.</summary>
public enum ThreatSeverity
{
    /// <summary>Worth recording. Acted on only in aggregate.</summary>
    Informational = 0,

    /// <summary>Worth looking at.</summary>
    Low = 1,

    /// <summary>Worth looking at soon.</summary>
    Medium = 2,

    /// <summary>Worth waking somebody.</summary>
    High = 3,

    /// <summary>Act now.</summary>
    Critical = 4
}

/// <summary>
/// One observation that might mean something.
/// <para>
/// <b>A signal is not a verdict, and the platform never treats one as a verdict.</b> Nothing here
/// blocks an account, refuses a payment or bans an address. Signals are published, counted, and made
/// available to a moderation or fraud process that has context this platform does not. A detector that
/// could lock accounts on its own would lock a member out of theirs the first time they travelled.
/// </para>
/// </summary>
/// <param name="Kind">What sort.</param>
/// <param name="Severity">How seriously.</param>
/// <param name="Detector">Which detector raised it.</param>
/// <param name="Subject">
/// What it is about — an account, an address, a device. <b>Pseudonymous.</b> Never an email, a phone
/// number or anything else that identifies a person directly: a threat log is read by more people than
/// the data it would otherwise contain should be.
/// </param>
/// <param name="Description">One line, for a human.</param>
/// <param name="ObservedAt">When.</param>
/// <param name="CountryCode">Where, when that is known.</param>
/// <param name="Evidence">
/// Counts and comparisons that justify the signal. Numbers and enumerated values only — never a
/// credential, a token, a payload or free text a member wrote.
/// </param>
public sealed record ThreatSignal(
    ThreatKind Kind,
    ThreatSeverity Severity,
    string Detector,
    string Subject,
    string Description,
    DateTimeOffset ObservedAt,
    string? CountryCode = null,
    IReadOnlyDictionary<string, object>? Evidence = null);

/// <summary>
/// Raises threat signals.
/// <para>
/// Implemented per detection concern and registered by whoever owns that concern. The platform ships
/// the ones that can be computed from what it already observes — request rates, failed authentications,
/// notification volumes — and leaves the ones needing business context to the modules that have it.
/// </para>
/// </summary>
public interface IThreatDetector
{
    /// <summary>The detector's name. Appears on every signal it raises.</summary>
    string Name { get; }

    /// <summary>What it looks for.</summary>
    ThreatKind Kind { get; }

    /// <summary>
    /// Evaluates whatever it watches and returns whatever it found.
    /// <para>
    /// Pull rather than push, called on a schedule. A detector that pushed from a request path would be
    /// on that path's latency budget, and the ones worth having need to compare against a window rather
    /// than against one event.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ThreatSignal>> DetectAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Where signals go.
/// <para>
/// Separate from the detector so a deployment can route signals to a SIEM, to the notification
/// platform, to both, or to neither, without a detector knowing. The platform ships a sink that records
/// and counts them.
/// </para>
/// </summary>
public interface IThreatSignalSink
{
    Task PublishAsync(ThreatSignal signal, CancellationToken cancellationToken = default);
}

/// <summary>
/// The DDoS extension point.
/// <para>
/// <b>Deliberately a hook and not an implementation.</b> Volumetric attacks are absorbed in front of
/// the application — at the edge, by a provider with capacity the application does not have — and an
/// in-process mitigation is a process spending its last resources deciding which requests to drop.
/// What the application can usefully do is tell the edge what it is seeing, and act on what the edge
/// tells it; that is what this is for.
/// </para>
/// </summary>
public interface IDdosMitigationHook
{
    /// <summary>The hook's name, for diagnostics.</summary>
    string Name { get; }

    /// <summary>
    /// Called when the platform's own signals suggest an attack. An implementation might raise an edge
    /// rule, tighten a limit, or do nothing but record.
    /// </summary>
    Task OnAttackSuspectedAsync(ThreatSignal signal, CancellationToken cancellationToken = default);

    /// <summary>Called when the condition clears.</summary>
    Task OnAttackClearedAsync(string subject, CancellationToken cancellationToken = default);
}
