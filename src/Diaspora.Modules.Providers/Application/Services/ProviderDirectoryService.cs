using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Modules.Providers.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// The read surface other modules use to look providers up.
/// <para>
/// <b>The class that makes the brief's central promise real.</b> Marketplace, Jobs, Housing,
/// Events and Community Life each store a <c>ProviderId</c> and ask here, rather than growing a
/// provider table of their own. Everything they need — resolve, render a card, check a role,
/// confirm a badge, ask whether it is open — is on this one interface, and nothing else of B7 is
/// public to them.
/// </para>
/// <para>
/// Every method is batched or single-fetch. There is no "list every provider", because a
/// vertical that wants to iterate the directory is about to do something B7 should be doing for
/// it — and would do better, because it owns the indexes.
/// </para>
/// </summary>
public sealed class ProviderDirectoryService : IProviderDirectory
{
    private readonly IProviderRepository _providers;
    private readonly ServiceCategoryResolver _categories;
    private readonly AvailabilityCalculator _availability;

    public ProviderDirectoryService(
        IProviderRepository providers,
        ServiceCategoryResolver categories,
        AvailabilityCalculator availability)
    {
        _providers = providers;
        _categories = categories;
        _availability = availability;
    }

    public async Task<ProviderSummary?> FindAsync(
        ProviderId providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetForReadAsync(providerId, cancellationToken);

        return provider is null ? null : await ToSummaryAsync(provider, cancellationToken);
    }

    public async Task<ProviderSummary?> FindBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var parsed = ProviderSlug.Create(slug);

        if (parsed.IsFailure)
        {
            return null;
        }

        var provider = await _providers.GetBySlugAsync(parsed.Value, cancellationToken);

        return provider is null ? null : await ToSummaryAsync(provider, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ProviderSummary>> FindManyAsync(
        IReadOnlyCollection<ProviderId> providerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, ProviderSummary>();
        }

        var providers = await _providers.GetManyAsync(providerIds, cancellationToken);

        // One category lookup for the whole batch, not one per provider. The resolver caches for
        // the request, so a consuming vertical rendering fifty listings costs one extra query.
        var categories = await _categories.LookupAsync(
            providers.Select(provider => provider.CategoryId).ToList(), cancellationToken);

        return providers.ToDictionary(
            provider => provider.Id.Value,
            provider => ToSummary(provider, categories));
    }

    public async Task<bool> HasRoleAsync(
        UserId userId,
        ProviderId providerId,
        ProviderRole minimumRole,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var roles = await _providers.GetRolesForAsync(userId, [providerId], cancellationToken);

        return roles.TryGetValue(providerId.Value, out var role) && role >= minimumRole;
    }

    public async Task<IReadOnlyList<ProviderSummary>> ListForMemberAsync(
        UserId userId,
        ProviderRole minimumRole = ProviderRole.Editor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var providers = await _providers.ListForMemberAsync(
            userId, Domain.Policies.ProviderLimits.MaxProvidersPerOwner, cancellationToken);

        var eligible = providers
            .Where(provider => provider.HasAtLeast(userId, minimumRole))
            .ToList();

        var categories = await _categories.LookupAsync(
            eligible.Select(provider => provider.CategoryId).ToList(), cancellationToken);

        return eligible.Select(provider => ToSummary(provider, categories)).ToList();
    }

    public async Task<bool> IsVerifiedAsync(
        ProviderId providerId,
        ProviderVerificationKind? kind = null,
        CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetForReadAsync(providerId, cancellationToken);

        if (provider is null)
        {
            return false;
        }

        if (kind is null)
        {
            return provider.IsVerified;
        }

        // Expiry is evaluated against now rather than trusted from the stored state, so a grant
        // that lapsed overnight stops answering true this morning.
        var now = DateTimeOffset.UtcNow;

        return provider.Verifications.Any(claim => claim.Kind == kind && claim.IsCurrent(now));
    }

    public async Task<bool?> IsOpenNowAsync(
        ProviderId providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetForReadAsync(providerId, cancellationToken);

        if (provider is null)
        {
            return null;
        }

        var answer = await _availability.EvaluateAsync(
            provider, cancellationToken: cancellationToken);

        return answer.IsOpen;
    }

    private async Task<ProviderSummary> ToSummaryAsync(
        Provider provider, CancellationToken cancellationToken)
    {
        var categories = await _categories.LookupAsync([provider.CategoryId], cancellationToken);

        return ToSummary(provider, categories);
    }

    private static ProviderSummary ToSummary(
        Provider provider, IReadOnlyDictionary<Guid, Domain.Categories.ServiceCategory> categories)
    {
        var category = categories.GetValueOrDefault(provider.CategoryId.Value);

        return new ProviderSummary
        {
            ProviderId = provider.Id.Value,
            Slug = provider.Slug.Value,
            Name = provider.Name.Value,
            ProviderType = ProviderWireNames.ToWire(provider.Type),
            Status = ProviderWireNames.ToWire(provider.Status),
            CategoryId = provider.CategoryId.Value,

            // A retired category still has to render a name. Failing the whole lookup because a
            // category was retired without moving its providers would take a consuming vertical's
            // listing page down over a data problem it did not cause.
            CategoryName = category?.Name ?? "Uncategorised",
            CategorySlug = category?.Slug.Value,
            IsVerified = provider.IsVerified,
            IsFeatured = provider.IsFeatured,
            AverageRating = provider.ReviewSummary.Average,
            TotalReviews = provider.ReviewSummary.TotalReviews,
            CountryCode = provider.Location.CountryCode,
            AreaId = provider.Location.AreaId,
            AreaName = provider.Location.AreaName,
            CityName = provider.Location.CityName,
            Latitude = provider.Location.Latitude,
            Longitude = provider.Location.Longitude,
            CoverMediaId = provider.Profile.CoverMediaId?.Value,
            LogoMediaId = provider.Profile.LogoMediaId?.Value,
            OwnerId = provider.PrimaryOwnerId.Value
        };
    }
}
