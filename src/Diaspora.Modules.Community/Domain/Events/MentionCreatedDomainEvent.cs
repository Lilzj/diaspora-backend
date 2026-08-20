using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a mention resolves to a real target.
/// <para>
/// Only resolved mentions raise this. An unresolved handle is stored so the body renders
/// faithfully, but there is nobody to notify, and publishing an event with a null target would
/// make every consumer implement the same null check.
/// </para>
/// </summary>
public sealed record MentionCreatedDomainEvent : DomainEvent
{
    public required MentionId MentionId { get; init; }

    public required ContentReference Content { get; init; }

    public required UserId MentionedBy { get; init; }

    public required MentionTargetType TargetType { get; init; }

    public required Guid TargetId { get; init; }
}
