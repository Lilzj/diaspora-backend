using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Domain.Rsvps;

/// <summary>
/// One step in an RSVP's journey.
/// <para>
/// <b>The brief asks for "complete history" and this is it.</b> An RSVP that went
/// Interested → Going → Cancelled says something an RSVP that is merely Cancelled does not, and the
/// no-show rate the brief also asks for is uncomputable without knowing whether somebody withdrew
/// in advance or simply failed to appear.
/// </para>
/// </summary>
public sealed class RsvpHistoryEntry
{
    private RsvpHistoryEntry(
        Guid entryId,
        RsvpStatus status,
        UserId? changedBy,
        string? reason,
        DateTimeOffset occurredAt)
    {
        EntryId = entryId;
        Status = status;
        ChangedBy = changedBy;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    private RsvpHistoryEntry() { }

    public Guid EntryId { get; private set; }

    public RsvpStatus Status { get; private set; }

    /// <summary>Who made the change. Null when the attendee made it themselves.</summary>
    public UserId? ChangedBy { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static RsvpHistoryEntry Create(
        Guid entryId,
        RsvpStatus status,
        UserId? changedBy,
        string? reason,
        DateTimeOffset occurredAt)
    {
        var trimmed = reason?.Trim();

        if (trimmed is { Length: > CommunityLifeLimits.MaxNoteLength })
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxNoteLength];
        }

        return new RsvpHistoryEntry(
            entryId,
            status,
            changedBy,
            string.IsNullOrEmpty(trimmed) ? null : trimmed,
            occurredAt);
    }
}
