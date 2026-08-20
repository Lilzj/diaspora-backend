using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>Raised when a media asset is detached from a posting.</summary>
public sealed record JobPostingMediaDetachedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required Guid MediaId { get; init; }

    public required JobMediaRole Role { get; init; }
}
