using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>Raised when content is passed along, inside or outside the platform.</summary>
public sealed record ShareRecordedDomainEvent : DomainEvent
{
    public required ShareId ShareId { get; init; }

    public required ContentReference Content { get; init; }

    public required UserId SharedBy { get; init; }

    public required ShareKind Kind { get; init; }

    public Guid? ContentAuthorId { get; init; }

    public Guid? RecipientId { get; init; }
}
