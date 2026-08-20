namespace Diaspora.Platform.Services.Models;

public sealed record AuditEntry
{
    public required string Action { get; init; }
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public string? ActorId { get; init; }
    public string? ActorRole { get; init; }
    public Dictionary<string, object?>? OldValues { get; init; }
    public Dictionary<string, object?>? NewValues { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
    public string? CorrelationId { get; init; }
}
