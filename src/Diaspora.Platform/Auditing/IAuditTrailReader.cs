namespace Diaspora.Platform.Auditing;

/// <summary>What the trail is read for: one thing's history, one person's actions, or a window.</summary>
public sealed record AuditTrailQuery
{
    public string? EntityId { get; init; }

    public string? ActorUserId { get; init; }

    /// <summary>An action name, or the start of one: <c>identity.</c> matches every identity action.</summary>
    public string? Action { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public int Limit { get; init; } = 50;
}

/// <summary>Reads the stored audit trail back, newest first.</summary>
public interface IAuditTrailReader
{
    Task<IReadOnlyList<AuditRecord>> ReadAsync(
        AuditTrailQuery query, CancellationToken cancellationToken = default);
}
