using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Mentions;

/// <summary>
/// A record that somebody was mentioned.
/// <para>
/// Separate from the rendering records the post and comment aggregates carry, and both are
/// needed. Those answer "where in this body is the link"; this answers "who has been mentioned,
/// by whom, in what, and when" — which is the query a notification module, an abuse
/// investigation and a mentions-of-me screen all make, and none of them can be served by
/// scanning every post body on the platform.
/// </para>
/// <para>
/// Only resolved mentions are stored here. An unresolved handle stays on the body so it renders
/// faithfully, but there is nobody to notify and nothing to index.
/// </para>
/// </summary>
public sealed class Mention : AggregateRoot<MentionId>, IAuditableEntity
{
    private Mention(
        MentionId id,
        ContentReference content,
        UserId mentionedBy,
        MentionTargetType targetType,
        Guid targetId,
        DateTimeOffset mentionedAt) : base(id)
    {
        Content = content;
        MentionedBy = mentionedBy;
        TargetType = targetType;
        TargetId = targetId;
        MentionedAt = mentionedAt;
    }

    private Mention() { }

    public ContentReference Content { get; private set; } = default!;

    public UserId MentionedBy { get; private set; } = default!;

    public MentionTargetType TargetType { get; private set; }

    public Guid TargetId { get; private set; }

    public DateTimeOffset MentionedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static Mention Create(
        ContentReference content, UserId mentionedBy, MentionTarget target, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(mentionedBy);
        ArgumentNullException.ThrowIfNull(target);

        if (!target.IsResolved)
        {
            throw new InvalidOperationException(
                "An unresolved mention has no target and must not be stored.");
        }

        var mention = new Mention(
            MentionId.New(), content, mentionedBy, target.Type, target.TargetId!.Value, now);

        mention.RaiseDomainEvent(new MentionCreatedDomainEvent
        {
            MentionId = mention.Id,
            Content = content,
            MentionedBy = mentionedBy,
            TargetType = target.Type,
            TargetId = target.TargetId.Value
        });

        return mention;
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
