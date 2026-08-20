namespace Diaspora.Platform.Messaging.Outbox.Model;

/// <summary>
/// Column widths, declared once and shared by the model and the EF configuration.
/// <para>
/// Kept here rather than in the persistence configuration so the truncation that protects a
/// column and the width of that column cannot drift apart. A model that truncates at 4000
/// against a column declared at 2000 fails at insert time, inside a transaction that was
/// about to commit an aggregate — the single worst place for a length check to be wrong.
/// </para>
/// </summary>
public static class OutboxFieldLimits
{
    /// <summary>Contract names are namespace-qualified type names; 512 is generous.</summary>
    public const int EventType = 512;

    public const int Module = 128;

    public const int PartitionKey = 256;

    public const int TenantId = 128;

    public const int CorrelationId = 128;

    public const int CausationId = 128;

    /// <summary>Worker identity is host plus process; 128 covers a container name.</summary>
    public const int WorkerId = 128;

    /// <summary>
    /// Enough for a message and the top of a stack trace, short enough that a retry storm
    /// cannot inflate the table faster than the payloads themselves.
    /// </summary>
    public const int Error = 4000;

    public const int Reason = 256;
}
