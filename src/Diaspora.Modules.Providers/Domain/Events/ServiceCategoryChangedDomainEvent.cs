using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when the service category catalogue changed shape.
/// <para>
/// Rare, and consequential when it happens: a category renamed changes every provider card that
/// shows it, a category retired has to stop being offered in the filter row, and a category
/// moved changes the subtree every containment query resolves. Consumers holding a cached
/// category tree — which every client does, because the filter row renders before anything else
/// — invalidate on this.
/// </para>
/// </summary>
public sealed record ServiceCategoryChangedDomainEvent : DomainEvent
{
    public required ServiceCategoryId CategoryId { get; init; }

    public required string Slug { get; init; }

    /// <summary><c>created</c>, <c>renamed</c>, <c>moved</c>, <c>retired</c>, <c>reinstated</c>.</summary>
    public required string Change { get; init; }

    public ServiceCategoryId? ParentId { get; init; }
}
