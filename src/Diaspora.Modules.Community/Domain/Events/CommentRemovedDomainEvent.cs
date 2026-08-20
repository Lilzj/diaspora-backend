using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a comment stops being readable, by its author or by a moderator.
/// <para>
/// The comment row survives — a removed comment with replies is tombstoned so the thread keeps
/// its shape — so this event means "stop indexing it", not "forget it existed".
/// </para>
/// </summary>
public sealed record CommentRemovedDomainEvent : DomainEvent
{
    public required CommentId CommentId { get; init; }

    public required ContentReference Content { get; init; }

    public required CommentStatus Status { get; init; }

    public required bool ByModerator { get; init; }
}
