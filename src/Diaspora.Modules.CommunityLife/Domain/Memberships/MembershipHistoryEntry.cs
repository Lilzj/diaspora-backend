using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Domain.Memberships;

/// <summary>
/// One step in a membership's history.
/// <para>
/// <b>The history is kept, not just the current status.</b> "Was a member, left, came back" is a
/// different fact from "is a member", and an organization deciding whether to readmit somebody it
/// removed two years ago needs to be able to see that it did. It is also the only defence against
/// the argument that follows every contested removal.
/// </para>
/// </summary>
public sealed class MembershipHistoryEntry
{
    private MembershipHistoryEntry(
        Guid entryId,
        MembershipStatus status,
        MembershipRole role,
        UserId? changedBy,
        string? reason,
        DateTimeOffset occurredAt)
    {
        EntryId = entryId;
        Status = status;
        Role = role;
        ChangedBy = changedBy;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    private MembershipHistoryEntry() { }

    public Guid EntryId { get; private set; }

    public MembershipStatus Status { get; private set; }

    public MembershipRole Role { get; private set; }

    /// <summary>Who made the change. Null when the member made it themselves.</summary>
    public UserId? ChangedBy { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static MembershipHistoryEntry Create(
        Guid entryId,
        MembershipStatus status,
        MembershipRole role,
        UserId? changedBy,
        string? reason,
        DateTimeOffset occurredAt)
    {
        var trimmed = reason?.Trim();

        if (trimmed is { Length: > CommunityLifeLimits.MaxNoteLength })
        {
            trimmed = trimmed[..CommunityLifeLimits.MaxNoteLength];
        }

        return new MembershipHistoryEntry(
            entryId,
            status,
            role,
            changedBy,
            string.IsNullOrEmpty(trimmed) ? null : trimmed,
            occurredAt);
    }
}
