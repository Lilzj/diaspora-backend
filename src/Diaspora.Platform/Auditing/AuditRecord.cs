using System.Text.Json;
using Diaspora.Platform.Services.Models;

namespace Diaspora.Platform.Auditing;

/// <summary>
/// One stored entry in the audit trail: who did what, to which thing, and why.
/// <para>
/// <b>Until 7 November 2026 there was no trail.</b> Three hundred and eighty-one commands carried
/// <c>[Auditable]</c>, and every one of them wrote a log line and nothing else: no table, an empty
/// entity id, no reason, and the line written even when the action failed. A staff member could
/// suspend a member, hide their job or delete their file, and the only record was a log that rolls
/// over. The owner's rule for staff power needs a record that can be read back, so this is it.
/// </para>
/// <para>
/// Written in the same transaction as the action it records, and only when the action succeeded:
/// a commit that fails takes its audit row with it, and a refused action leaves nothing to explain
/// away.
/// </para>
/// </summary>
public sealed class AuditRecord
{
    public const int ActionLength = 128;
    public const int EntityTypeLength = 128;
    public const int EntityIdLength = 128;
    public const int ActorLength = 64;
    public const int RoleLength = 64;
    public const int ReasonLength = 2000;
    public const int DetailLength = 4000;
    public const int CorrelationLength = 128;

    private static readonly JsonSerializerOptions DetailJson = new(JsonSerializerDefaults.Web);

    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Who acted. Null for the system: a sweep, a threshold, a consumer.</summary>
    public string? ActorUserId { get; private set; }

    public string? ActorRole { get; private set; }

    public string Action { get; private set; } = default!;

    public string EntityType { get; private set; } = default!;

    /// <summary>What was acted on. Null only when the request named nothing.</summary>
    public string? EntityId { get; private set; }

    /// <summary>The reason the actor gave, when the action takes one.</summary>
    public string? Reason { get; private set; }

    /// <summary>Anything else the writer recorded, as JSON: metadata, and values before and after.</summary>
    public string? Detail { get; private set; }

    public string? CorrelationId { get; private set; }

    private AuditRecord() { }

    public static AuditRecord From(AuditEntry entry, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var detail = entry.Metadata is { Count: > 0 } || entry.OldValues is { Count: > 0 }
            || entry.NewValues is { Count: > 0 }
            ? JsonSerializer.Serialize(
                new { metadata = entry.Metadata, before = entry.OldValues, after = entry.NewValues },
                DetailJson)
            : null;

        return new AuditRecord
        {
            Id = Guid.NewGuid(),
            OccurredAt = at,
            ActorUserId = Clip(entry.ActorId, ActorLength),
            ActorRole = Clip(entry.ActorRole, RoleLength),
            Action = Clip(entry.Action, ActionLength) ?? "unknown",
            EntityType = Clip(entry.EntityType, EntityTypeLength) ?? "unknown",
            EntityId = Clip(entry.EntityId, EntityIdLength),
            Reason = Clip(entry.Reason, ReasonLength),
            Detail = Clip(detail, DetailLength),
            CorrelationId = Clip(entry.CorrelationId, CorrelationLength)
        };
    }

    private static string? Clip(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= length ? trimmed : trimmed[..length];
    }
}
