using System.Runtime.CompilerServices;
using System.Text;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Extensibility.Search;
using Diaspora.Modules.Providers.Integration;
using Diaspora.Modules.Providers.Observability;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Providers.Application.Services;

/// <summary>
/// Builds the documents a search engine would index. B7 builds them and stops.
/// <para>
/// <b>The line the brief draws — "expose provider search projections… do not implement
/// search" — falls exactly here.</b> Everything a search engine needs about a provider is
/// assembled into one denormalised, serialisable record, and nothing that tokenises, scores,
/// facets or embeds it lives anywhere in this module. B15 ingests these and owns everything
/// after.
/// </para>
/// <para>
/// <b>Denormalised heavily, on purpose.</b> Category ancestors, keywords, service names,
/// certification names and language codes are all copied in, because a search index cannot join
/// and because reindexing one document must not require loading six aggregates. That is the
/// trade a projection exists to make.
/// </para>
/// <para>
/// <see cref="ProviderSearchProjection.SemanticText"/> is assembled here rather than left to
/// B17, so that two modules embedding the same provider embed the same text. Two slightly
/// different concatenations produce two embeddings that are not comparable, and nothing about
/// the result would look wrong.
/// </para>
/// </summary>
public sealed class ProviderSearchProjectionService : IProviderSearchProjectionSource
{
    private readonly IProviderRepository _providers;
    private readonly ServiceCategoryResolver _categories;
    private readonly IEnumerable<IProviderSearchProjectionContributor> _contributors;
    private readonly IClock _clock;
    private readonly ILogger<ProviderSearchProjectionService> _logger;

    public ProviderSearchProjectionService(
        IProviderRepository providers,
        ServiceCategoryResolver categories,
        IEnumerable<IProviderSearchProjectionContributor> contributors,
        IClock clock,
        ILogger<ProviderSearchProjectionService> logger)
    {
        _providers = providers;
        _categories = categories;
        _contributors = contributors;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ProviderSearchProjection?> ProjectAsync(
        ProviderId providerId, CancellationToken cancellationToken = default)
    {
        var provider = await _providers.GetForReadAsync(providerId, cancellationToken);

        if (provider is null || !IsIndexable(provider))
        {
            return null;
        }

        var categories = await _categories.LookupAsync([provider.CategoryId], cancellationToken);
        var ancestors = await AncestorsAsync(provider, categories, cancellationToken);

        return await BuildAsync(provider, categories, ancestors, cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderSearchProjection>> ProjectManyAsync(
        IReadOnlyCollection<ProviderId> providerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        if (providerIds.Count == 0)
        {
            return [];
        }

        var providers = (await _providers.GetManyAsync(providerIds, cancellationToken))
            .Where(IsIndexable)
            .ToList();

        var categories = await _categories.LookupAsync(
            providers.Select(provider => provider.CategoryId).ToList(), cancellationToken);

        var projections = new List<ProviderSearchProjection>(providers.Count);

        foreach (var provider in providers)
        {
            var ancestors = await AncestorsAsync(provider, categories, cancellationToken);

            projections.Add(await BuildAsync(provider, categories, ancestors, cancellationToken));
        }

        return projections;
    }

    /// <summary>
    /// Every indexable provider, in batches.
    /// <para>
    /// Cursor-paged rather than offset-paged, because an offset sweep over a table being written
    /// to skips and repeats rows — which in an index rebuild means providers that silently never
    /// get indexed at all, and no error anywhere.
    /// </para>
    /// </summary>
    public async IAsyncEnumerable<ProviderSearchProjection> StreamAsync(
        int batchSize = 200,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;

        do
        {
            var page = await _providers.QueryAsync(
                new ProviderQueryFilter
                {
                    Statuses = [ProviderStatus.Active],
                    AllowedVisibility =
                    [
                        ProviderVisibilityLevel.Public,
                        ProviderVisibilityLevel.Members,
                        ProviderVisibilityLevel.CountryCommunity,
                        ProviderVisibilityLevel.AreaOnly
                    ],
                    Sort = ProviderSortOrder.Newest,
                    Cursor = cursor,
                    Limit = Math.Clamp(batchSize, 1, Domain.Policies.ProviderLimits.MaxPageSize)
                },
                cancellationToken);

            var categories = await _categories.LookupAsync(
                page.Items.Select(provider => provider.CategoryId).ToList(), cancellationToken);

            foreach (var provider in page.Items)
            {
                var ancestors = await AncestorsAsync(provider, categories, cancellationToken);

                yield return await BuildAsync(provider, categories, ancestors, cancellationToken);
            }

            cursor = page.NextCursor;
        }
        while (cursor is not null);
    }

    /// <summary>
    /// A provider is indexable when it is active and discoverable. Unlisted and private ones are
    /// excluded here rather than filtered downstream, because a document that reaches an index is
    /// a document that can leak out of one.
    /// </summary>
    private static bool IsIndexable(Provider provider) => provider.IsDiscoverable;

    private async Task<ProviderSearchProjection> BuildAsync(
        Provider provider,
        IReadOnlyDictionary<Guid, ServiceCategory> categories,
        IReadOnlyList<ServiceCategory> ancestors,
        CancellationToken cancellationToken)
    {
        var category = categories.GetValueOrDefault(provider.CategoryId.Value);
        var signals = new Dictionary<string, string>(StringComparer.Ordinal);

        await ContributeAsync(provider, signals, cancellationToken);

        var projection = new ProviderSearchProjection
        {
            ProviderId = provider.Id.Value,
            Slug = provider.Slug.Value,
            Name = provider.Name.Value,
            Summary = provider.Profile.Description.Summary,
            Description = provider.Profile.Description.Body,
            ProviderType = ProviderWireNames.ToWire(provider.Type),
            Status = ProviderWireNames.ToWire(provider.Status),
            CategoryId = provider.CategoryId.Value,
            CategoryName = category?.Name ?? "Uncategorised",
            CategoryPath = category?.Path.Value ?? "/",
            CategoryAncestors = ancestors.Select(node => node.Name).ToList(),
            CategoryKeywords = ancestors
                .SelectMany(node => node.Keywords.Select(keyword => keyword.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Tags = provider.Tags.Select(tag => tag.Label).ToList(),
            Languages = provider.Languages.Select(language => language.Code).ToList(),
            ServiceNames = provider.Services.Select(service => service.Name).ToList(),
            Certifications = provider.Certifications.Select(item => item.Name).ToList(),
            CountryCode = provider.Location.CountryCode,
            AreaId = provider.Location.AreaId,
            AreaName = provider.Location.AreaName,
            CityName = provider.Location.CityName,
            HierarchyPath = provider.Location.HierarchyPath,
            Latitude = provider.Location.Latitude,
            Longitude = provider.Location.Longitude,
            ServiceAreaCountries = provider.ServiceAreas
                .Where(area => area.CountryCode is not null)
                .Select(area => area.CountryCode!)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            ServiceAreaPaths = provider.ServiceAreas
                .Where(area => area.HierarchyPath is not null)
                .Select(area => area.HierarchyPath!)
                .Distinct(StringComparer.Ordinal)
                .ToList(),
            ServesRemotely = provider.ServesRemotely,
            DeliveryMode = ProviderWireNames.ToWire(provider.DeliveryMode),
            IsVerified = provider.IsVerified,
            VerifiedKinds = provider.Verifications
                .Where(claim => claim.IsCurrent(_clock.UtcNow))
                .Select(claim => ProviderWireNames.ToWire(claim.Kind))
                .ToList(),
            IsFeatured = provider.IsFeatured,
            AverageRating = provider.ReviewSummary.Average,
            TotalReviews = provider.ReviewSummary.TotalReviews,
            RecommendationCount = provider.Statistics.RecommendationCount,
            ProfileCompletionPercent = provider.Statistics.ProfileCompletionPercent,
            ViewCount = provider.Statistics.ViewCount,
            BookmarkCount = provider.Statistics.BookmarkCount,
            PublishedAt = provider.PublishedAt,
            UpdatedAt = provider.ModifiedAt ?? provider.CreatedAt,
            Signals = signals,
            SemanticText = BuildSemanticText(provider, category, ancestors)
        };

        ProviderTelemetry.SearchProjectionsGenerated.Add(1);

        return projection;
    }

    /// <summary>
    /// Runs the registered contributors, isolating each.
    /// <para>
    /// A contributor that throws must not fail the projection: a jobs module briefly unable to
    /// say whether a provider is hiring should cost that one signal, not the whole document. A
    /// missing document means a provider that vanishes from search.
    /// </para>
    /// </summary>
    private async Task ContributeAsync(
        Provider provider, IDictionary<string, string> signals, CancellationToken cancellationToken)
    {
        foreach (var contributor in _contributors.OrderBy(candidate => candidate.Order))
        {
            try
            {
                await contributor.ContributeAsync(provider, signals, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // One contributor's failure costs one signal, not the document.
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Search projection contributor {Module} threw for provider {ProviderId}.",
                    contributor.Module,
                    provider.Id.Value);
            }
#pragma warning restore CA1031
        }
    }

    private async Task<IReadOnlyList<ServiceCategory>> AncestorsAsync(
        Provider provider,
        IReadOnlyDictionary<Guid, ServiceCategory> categories,
        CancellationToken cancellationToken)
    {
        if (!categories.TryGetValue(provider.CategoryId.Value, out var category))
        {
            return [];
        }

        var all = await _categories.ListAsync(cancellationToken);
        var bySlug = all.ToDictionary(node => node.Slug.Value, StringComparer.Ordinal);

        var chain = category.Path.Segments
            .Select(slug => bySlug.GetValueOrDefault(slug))
            .Where(node => node is not null)
            .Select(node => node!)
            .ToList();

        // The node itself is always included, even when it has been retired out of the active
        // tree — its keywords are still the best signal for the providers filed under it.
        if (!chain.Exists(node => node.Id == category.Id))
        {
            chain.Add(category);
        }

        return chain;
    }

    /// <summary>
    /// One block of everything about the provider that reads as prose.
    /// <para>
    /// Assembled once, here, so an embedding model has a single field to consume and every
    /// consumer embeds the same text. Ordered from most to least identifying, because a model
    /// with a token budget truncates from the end.
    /// </para>
    /// </summary>
    private static string BuildSemanticText(
        Provider provider,
        ServiceCategory? category,
        IReadOnlyList<ServiceCategory> ancestors)
    {
        var builder = new StringBuilder();

        builder.Append(provider.Name.Value).Append(". ");

        if (category is not null)
        {
            builder.Append(string.Join(" › ", ancestors.Select(node => node.Name))).Append(". ");
        }

        if (provider.Profile.Tagline is { Length: > 0 } tagline)
        {
            builder.Append(tagline).Append(". ");
        }

        if (provider.Profile.Description.Summary is { Length: > 0 } summary)
        {
            builder.Append(summary).Append(' ');
        }

        if (provider.Location.Describe() is { Length: > 0 } place)
        {
            builder.Append("Located in ").Append(place).Append(". ");
        }

        if (provider.Services.Count > 0)
        {
            builder
                .Append("Services: ")
                .Append(string.Join(", ", provider.Services.Select(service => service.Name)))
                .Append(". ");
        }

        if (provider.Tags.Count > 0)
        {
            builder
                .Append(string.Join(", ", provider.Tags.Select(tag => tag.Label)))
                .Append(". ");
        }

        if (provider.Languages.Count > 0)
        {
            builder
                .Append("Languages: ")
                .Append(string.Join(", ", provider.Languages.Select(language => language.Code)))
                .Append(". ");
        }

        return builder.ToString().TrimEnd();
    }
}
