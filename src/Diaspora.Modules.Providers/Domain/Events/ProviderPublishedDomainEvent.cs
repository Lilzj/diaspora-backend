using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.Events;

/// <summary>
/// Raised the first time a provider becomes visible to somebody other than its own staff.
/// <para>
/// The event every downstream consumer actually wants: this is when a provider enters search
/// (B15), becomes recommendable (B17), and is worth telling a neighbourhood about (B16). A
/// provider that is published, paused and published again raises this once — republication is a
/// <see cref="ProviderStatusChangedDomainEvent"/>, because the second time is not news.
/// </para>
/// </summary>
public sealed record ProviderPublishedDomainEvent : DomainEvent
{
    public required ProviderId ProviderId { get; init; }

    public required UserId OwnerId { get; init; }

    public required ProviderType ProviderType { get; init; }

    public required ServiceCategoryId CategoryId { get; init; }

    public required string Name { get; init; }

    public required ProviderVisibilityLevel Visibility { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}
