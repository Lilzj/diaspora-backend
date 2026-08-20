using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised when a provider is created, before it is visible to anyone.
/// <para>
/// Separate from <see cref="ProviderPublishedDomainEvent"/> on purpose. A registration is worth
/// auditing, worth counting and worth prompting the owner to finish — and it is emphatically not
/// worth indexing, recommending or notifying a neighbourhood about, because a draft is not a
/// business. Collapsing the two would put half-written providers into search.
/// </para>
/// </summary>
public sealed record ProviderRegisteredDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required UserId OwnerId { get; init; }

    public required ProviderType ProviderType { get; init; }

    public required ServiceCategoryId CategoryId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }
}
