namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// The metadata keys the platform itself writes.
/// <para>
/// Named constants rather than string literals at each call site because these keys are read
/// by the dispatcher, the administration API and eventually by a broker adapter, and a typo in
/// any one of them produces a message that looks correct and loses its correlation trail.
/// A transport or module may add its own keys freely; it must not reuse these.
/// </para>
/// </summary>
public static class OutboxMetadataKeys
{
    public const string CorrelationId = "diaspora.correlation-id";

    public const string CausationId = "diaspora.causation-id";

    public const string TenantId = "diaspora.tenant-id";

    public const string Module = "diaspora.module";

    public const string PartitionKey = "diaspora.partition-key";

    /// <summary>
    /// The contract version, duplicated into metadata so a transport that carries headers but
    /// not our envelope shape still delivers enough for a consumer to pick a reader.
    /// </summary>
    public const string EventVersion = "diaspora.event-version";

    /// <summary>Set on a message put back by an operator, so a replay is visible downstream.</summary>
    public const string ReplayedAt = "diaspora.replayed-at";
}
