using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Saved;

/// <summary>
/// A job a candidate put aside to come back to.
/// <para>
/// <b>Its own aggregate rather than Community's bookmark, and the difference is the note.</b>
/// B6 owns generic bookmarking and Jobs uses it for the shared "saved items" surface; this
/// exists alongside it because saving a job is part of a job hunt — the candidate wants a
/// private note ("ask about the shift pattern"), a reminder before the closing date, and a list
/// that sits beside their applications rather than beside their saved posts.
/// </para>
/// <para>
/// The pair (candidate, posting) is unique, which is what makes saving idempotent and unsaving
/// safe to retry.
/// </para>
/// </summary>
public sealed class SavedJob : AggregateRoot<SavedJobId>, IAuditableEntity, IHasOwner
{
    private SavedJob(
        SavedJobId id,
        CandidateProfileId candidateId,
        UserId userId,
        JobPostingId postingId,
        string? note,
        DateTimeOffset savedAt)
        : base(id)
    {
        CandidateId = candidateId;
        UserId = userId;
        PostingId = postingId;
        Note = note;
        CreatedAt = savedAt;
    }

    private SavedJob() { }

    public CandidateProfileId CandidateId { get; private set; } = default!;

    /// <summary>The member. Denormalised so an ownership check needs no join.</summary>
    public UserId UserId { get; private set; } = default!;

    /// <summary>The platform's ownership seam.</summary>
    public Guid OwnerId => UserId.Value;

    public JobPostingId PostingId { get; private set; } = default!;

    /// <summary>The candidate's private note. Never visible to the employer.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Whether the candidate asked to be reminded before the closing date. The one thing that
    /// turns a saved job from a list into something that actually helps somebody apply in time.
    /// </summary>
    public bool RemindBeforeClosing { get; private set; }

    /// <summary>When the reminder was sent, so it is sent once.</summary>
    public DateTimeOffset? ReminderSentAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<SavedJob> Create(
        SavedJobId id,
        CandidateProfileId candidateId,
        UserId userId,
        JobPostingId postingId,
        string? note,
        DateTimeOffset now,
        bool remindBeforeClosing = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(candidateId);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(postingId);

        var trimmed = Clip(note);

        if (note is not null && trimmed is null && note.Trim().Length > 0)
        {
            return Result.Failure<SavedJob>(JobErrors.SavedJobNotFound);
        }

        return Result.Success(new SavedJob(id, candidateId, userId, postingId, trimmed, now)
        {
            RemindBeforeClosing = remindBeforeClosing
        });
    }

    /// <summary>Updates the private note and the reminder preference.</summary>
    public Result Update(string? note, bool remindBeforeClosing)
    {
        Note = Clip(note);
        RemindBeforeClosing = remindBeforeClosing;

        // Turning the reminder back on after it fired should let it fire again for a posting
        // whose closing date the employer has since extended.
        if (!remindBeforeClosing)
        {
            ReminderSentAt = null;
        }

        return Result.Success();
    }

    /// <summary>Records that the closing-date reminder went out, so it goes out once.</summary>
    public void MarkReminderSent(DateTimeOffset now) => ReminderSentAt = now;

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= JobLimits.MaxSavedJobNote
            ? trimmed
            : trimmed[..JobLimits.MaxSavedJobNote].TrimEnd();
    }
}
