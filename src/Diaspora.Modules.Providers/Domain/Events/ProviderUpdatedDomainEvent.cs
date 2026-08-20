using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider's own descriptive or operational content changed.
/// <para>
/// <see cref="ChangedFields"/> is what makes this event usable rather than merely present. A
/// consumer that reindexes on every provider edit reindexes on a gallery reorder; one that reads
/// the field list reindexes only when something searchable moved. The names are the response
/// DTO's field names, so a consumer never has to know B7's internal property names.
/// </para>
/// </summary>
public sealed record ProviderUpdatedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    /// <summary>Who made the change. Null when a projection updated it from another module's event.</summary>
    public UserId? ChangedBy { get; init; }

    /// <summary>Wire field names that moved: <c>name</c>, <c>description</c>, <c>category</c>.</summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }

    /// <summary>
    /// The provider's longest-standing owner, stamped by the aggregate — B13.1. Null only when
    /// the provider was loaded without its membership.
    /// </summary>
    public UserId? OwnerUserId { get; init; }
}
