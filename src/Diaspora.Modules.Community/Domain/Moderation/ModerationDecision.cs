using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Moderation;

/// <summary>
/// A record that somebody decided something about a piece of content.
/// <para>
/// <b>Append-only.</b> Nothing edits a decision and nothing deletes one; a reversal is a new
/// decision that says restore. That is what makes the history an actual history — an appeal asks
/// what was decided, by whom, and on what basis, and a mutable decision row cannot answer any of
/// the three.
/// </para>
/// <para>
/// The previous and new status are captured as strings rather than as a typed status, because
/// the same table records decisions about posts and comments, whose statuses are different
/// enums. Storing the name keeps one table without inventing a union type that means nothing on
/// either side.
/// </para>
/// </summary>
public sealed class ModerationDecision : AggregateRoot<ModerationDecisionId>, IAuditableEntity
{
    public const int MaxReasonLength = 2_000;

    private ModerationDecision(
        ModerationDecisionId id,
        ContentReference content,
        ModerationActionType action,
        ModerationSource source,
        Guid? moderatorId,
        string? reason,
        string previousStatus,
        string newStatus,
        DateTimeOffset decidedAt) : base(id)
    {
        Content = content;
        Action = action;
        Source = source;
        ModeratorId = moderatorId;
        Reason = reason;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        DecidedAt = decidedAt;
    }

    private ModerationDecision() { }

    public ContentReference Content { get; private set; } = default!;

    public ModerationActionType Action { get; private set; }

    public ModerationSource Source { get; private set; }

    /// <summary>Null for an automated or system decision. Never null for a staff one.</summary>
    public Guid? ModeratorId { get; private set; }

    public string? Reason { get; private set; }

    public string PreviousStatus { get; private set; } = string.Empty;

    public string NewStatus { get; private set; } = string.Empty;

    /// <summary>The report this decision answers, when it answers one.</summary>
    public ContentReportId? ReportId { get; private set; }

    public Guid? ContentAuthorId { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static ModerationDecision Record(
        ContentReference content,
        ModerationActionType action,
        ModerationSource source,
        Guid? moderatorId,
        string? reason,
        string previousStatus,
        string newStatus,
        DateTimeOffset now,
        ContentReportId? reportId = null,
        Guid? contentAuthorId = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var trimmed = reason?.Trim();

        var decision = new ModerationDecision(
            ModerationDecisionId.New(),
            content,
            action,
            source,
            moderatorId,
            string.IsNullOrEmpty(trimmed)
                ? null
                : trimmed.Length > MaxReasonLength ? trimmed[..MaxReasonLength] : trimmed,
            previousStatus,
            newStatus,
            now)
        {
            ReportId = reportId,
            ContentAuthorId = contentAuthorId
        };

        decision.RaiseDomainEvent(new ModerationDecidedDomainEvent
        {
            DecisionId = decision.Id,
            Content = content,
            Action = action,
            Source = source,
            ModeratorId = moderatorId,
            ContentAuthorId = contentAuthorId,
            Reason = decision.Reason
        });

        return decision;
    }

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
}
