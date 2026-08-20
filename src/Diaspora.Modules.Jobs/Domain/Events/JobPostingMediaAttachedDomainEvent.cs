using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Events;

/// <summary>
/// Raised when a media asset is attached to a posting.
/// <para>
/// The handler for this is what tells Media the asset has been claimed. Without it the asset
/// stays in the orphan population and B5's sweeper deletes it a day later — silently, because
/// the upload succeeded and the attach succeeded.
/// </para>
/// </summary>
public sealed record JobPostingMediaAttachedDomainEvent : DomainEvent
{
    public required JobPostingId PostingId { get; init; }

    public required Guid MediaId { get; init; }

    public required JobMediaRole Role { get; init; }
}
