using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a member saves content.
/// <para>
/// The strongest positive signal a member gives without saying anything, and the reason this
/// event exists at all: a recommendation engine that only sees reactions sees applause, while
/// a save means somebody intends to come back.
/// </para>
/// </summary>
public sealed record BookmarkAddedDomainEvent : DomainEvent
{
    public required CommunityBookmarkId BookmarkId { get; init; }

    public required ContentReference Content { get; init; }

    public required UserId UserId { get; init; }

    public Guid? FolderId { get; init; }

    public Guid? ContentAuthorId { get; init; }
}
