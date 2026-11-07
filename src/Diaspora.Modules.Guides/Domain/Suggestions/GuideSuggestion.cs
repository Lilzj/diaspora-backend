using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Events;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Domain.Suggestions;

/// <summary>
/// One member's proposed improvement to a guide — the platform's lightweight merge request.
/// <para>
/// An aggregate of its own, keyed by <see cref="GuideId"/>, because a suggestion has a
/// lifecycle the guide does not share: it is submitted, discussed, decided and withdrawn by
/// people who are not the author, and a popular guide must not be loaded and locked to record
/// an opinion about it.
/// </para>
/// <para>
/// <b>Accepting does not edit the guide.</b> The decision is the workflow; the author applies
/// the change through the ordinary editor, where it gets the same validation, revision bump and
/// version history as any edit. Auto-applying community text to a published guide would be a
/// path onto readers' screens that bypasses the author, and the author's name is on the guide.
/// </para>
/// </summary>
public sealed class GuideSuggestion : AggregateRoot<GuideSuggestionId>, IAuditableEntity
{
    private GuideSuggestion(
        GuideSuggestionId id,
        GuideId guideId,
        UserId authorId,
        Guid? targetSectionId,
        string proposedContent,
        string? rationale,
        DateTimeOffset createdAt)
        : base(id)
    {
        GuideId = guideId;
        AuthorId = authorId;
        TargetSectionId = targetSectionId;
        ProposedContent = proposedContent;
        Rationale = rationale;
        Status = GuideSuggestionStatus.Open;
        CreatedAt = createdAt;
    }

    private GuideSuggestion() { }

    public GuideId GuideId { get; private set; } = default!;

    /// <summary>The suggester — not the guide's author, which the handler enforces.</summary>
    public UserId AuthorId { get; private set; } = default!;

    /// <summary>The section it targets, when it targets one. Null proposes to the whole guide.</summary>
    public Guid? TargetSectionId { get; private set; }

    public string ProposedContent { get; private set; } = string.Empty;

    /// <summary>Why the change should be made, in the suggester's words.</summary>
    public string? Rationale { get; private set; }

    public GuideSuggestionStatus Status { get; private set; }

    public UserId? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>What the decider told the suggester. Shown to both.</summary>
    public string? DecisionNote { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static Result<GuideSuggestion> Submit(
        GuideSuggestionId id,
        GuideId guideId,
        UserId authorId,
        string? proposedContent,
        DateTimeOffset now,
        Guid? targetSectionId = null,
        string? rationale = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(guideId);
        ArgumentNullException.ThrowIfNull(authorId);

        var content = proposedContent?.Trim();

        if (string.IsNullOrEmpty(content) || content.Length > GuideLimits.MaxSuggestionContent)
        {
            return Result.Failure<GuideSuggestion>(GuideErrors.BlockInvalid);
        }

        // Truncated rather than rejected: the rationale is supporting prose, and losing a tail
        // nobody will read is better than losing the suggestion.
        var trimmedRationale = rationale?.Trim();

        if (trimmedRationale is { Length: > GuideLimits.MaxSuggestionRationale })
        {
            trimmedRationale = trimmedRationale[..GuideLimits.MaxSuggestionRationale];
        }

        var suggestion = new GuideSuggestion(
            id,
            guideId,
            authorId,
            targetSectionId,
            content,
            string.IsNullOrEmpty(trimmedRationale) ? null : trimmedRationale,
            now);

        suggestion.RaiseDomainEvent(new GuideSuggestionSubmittedDomainEvent
        {
            SuggestionId = id,
            GuideId = guideId,
            AuthorId = authorId
        });

        return Result.Success(suggestion);
    }

    /// <summary>
    /// The guide's author accepts. The guide itself is untouched — applying the
    /// text is a separate, ordinary edit.
    /// </summary>
    public Result Accept(UserId decidedBy, DateTimeOffset now, string? note = null) =>
        Decide(GuideSuggestionStatus.Accepted, decidedBy, now, note);

    /// <summary>The guide's author declines, with an optional explanation.</summary>
    public Result Reject(UserId decidedBy, DateTimeOffset now, string? note = null) =>
        Decide(GuideSuggestionStatus.Rejected, decidedBy, now, note);

    /// <summary>The suggester takes it back. Only they can — a withdrawal is theirs to make.</summary>
    public Result Withdraw(UserId requestedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requestedBy);

        if (requestedBy != AuthorId)
        {
            return Result.Failure(GuideErrors.SuggestionNotAuthor);
        }

        if (Status != GuideSuggestionStatus.Open)
        {
            return Result.Failure(GuideErrors.SuggestionClosed);
        }

        Status = GuideSuggestionStatus.Withdrawn;
        DecidedBy = requestedBy;
        DecidedAt = now;

        RaiseDecided(requestedBy, now);

        return Result.Success();
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

    private Result Decide(
        GuideSuggestionStatus status, UserId decidedBy, DateTimeOffset now, string? note)
    {
        ArgumentNullException.ThrowIfNull(decidedBy);

        if (Status != GuideSuggestionStatus.Open)
        {
            return Result.Failure(GuideErrors.SuggestionClosed);
        }

        var trimmedNote = note?.Trim();

        Status = status;
        DecidedBy = decidedBy;
        DecidedAt = now;
        DecisionNote = string.IsNullOrEmpty(trimmedNote)
            ? null
            : trimmedNote[..Math.Min(trimmedNote.Length, GuideLimits.MaxSuggestionRationale)];

        RaiseDecided(decidedBy, now);

        return Result.Success();
    }

    private void RaiseDecided(UserId decidedBy, DateTimeOffset now) =>
        RaiseDomainEvent(new GuideSuggestionDecidedDomainEvent
        {
            SuggestionId = Id,
            GuideId = GuideId,
            Status = Status,
            DecidedBy = decidedBy,
            DecidedAt = now
        });
}
