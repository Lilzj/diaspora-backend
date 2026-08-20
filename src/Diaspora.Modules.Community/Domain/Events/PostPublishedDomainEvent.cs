using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Events;

/// <summary>
/// Raised when a post becomes visible to anybody other than its author.
/// <para>
/// Raised on publication rather than on creation, and the difference is not pedantic: a draft
/// is not content. Indexing a draft, recommending it, or notifying a mentioned member about it
/// would each expose something the author has not decided to say yet.
/// </para>
/// </summary>
public sealed record PostPublishedDomainEvent : DomainEvent
{
    public required CommunityPostId PostId { get; init; }

    public required UserId AuthorId { get; init; }

    public required PostCategory Category { get; init; }

    public required UrgencyLevel Urgency { get; init; }

    public required VisibilityLevel Visibility { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public Guid? OrganizationId { get; init; }
}
