using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>
/// One entry on an application's append-only history.
/// <para>
/// <b>The history is the point of the whole aggregate.</b> A status column tells a candidate
/// where they are; the history tells them how long they waited at each step, tells an employer
/// where their process actually loses people, and is the evidence if either disputes what
/// happened. It is appended and never edited or removed — there is no mutator on this type and
/// no removal path on the aggregate.
/// </para>
/// <para>
/// <b><see cref="IsVisibleToCandidate"/> is what makes an honest history possible.</b> An
/// employer's internal note — "strong, but we already have someone" — belongs on the record and
/// emphatically not in front of the candidate. Without the flag, the platform would have to
/// choose between hiding the timeline from candidates entirely and publishing every private
/// remark; both are worse.
/// </para>
/// </summary>
public sealed class ApplicationHistoryEntry
{
    private ApplicationHistoryEntry(
        Guid entryId,
        ApplicationEventKind kind,
        ApplicationStatus? fromStatus,
        ApplicationStatus? toStatus,
        UserId? actorId,
        string? note,
        bool isVisibleToCandidate,
        DateTimeOffset occurredAt)
    {
        EntryId = entryId;
        Kind = kind;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActorId = actorId;
        Note = note;
        IsVisibleToCandidate = isVisibleToCandidate;
        OccurredAt = occurredAt;
    }

    private ApplicationHistoryEntry() { }

    public Guid EntryId { get; private set; }

    public ApplicationEventKind Kind { get; private set; }

    /// <summary>Where the application was. Null when the entry is not a status move.</summary>
    public ApplicationStatus? FromStatus { get; private set; }

    /// <summary>Where it went. Null when the entry is not a status move.</summary>
    public ApplicationStatus? ToStatus { get; private set; }

    /// <summary>Who did it. Null when the clock or a sweep did.</summary>
    public UserId? ActorId { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Whether the candidate sees this entry on their own timeline.</summary>
    public bool IsVisibleToCandidate { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    internal static ApplicationHistoryEntry Create(
        Guid entryId,
        ApplicationEventKind kind,
        ApplicationStatus? fromStatus,
        ApplicationStatus? toStatus,
        UserId? actorId,
        string? note,
        bool isVisibleToCandidate,
        DateTimeOffset occurredAt)
    {
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        if (trimmed is { Length: > JobLimits.MaxApplicationNote })
        {
            trimmed = trimmed[..JobLimits.MaxApplicationNote];
        }

        return new ApplicationHistoryEntry(
            entryId == Guid.Empty ? Guid.NewGuid() : entryId,
            kind, fromStatus, toStatus, actorId, trimmed, isVisibleToCandidate, occurredAt);
    }
}
