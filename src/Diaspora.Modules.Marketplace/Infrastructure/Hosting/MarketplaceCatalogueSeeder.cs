using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace;
using Diaspora.Modules.Marketplace.Domain.Attributes;
using Diaspora.Modules.Marketplace.Domain.Catalog;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Extensibility.Attributes;
using Diaspora.Modules.Marketplace.Extensibility.Categories;
using Diaspora.Modules.Marketplace.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Marketplace.Infrastructure.Hosting;

/// <summary>
/// Seeds the category tree and the attribute catalogue from every registered contributor.
/// <para>
/// <b>Insert-if-absent, matching on key — never an upsert.</b> A category whose key already exists
/// is left entirely alone: not renamed, not reordered, not re-described. That is deliberate and it
/// is B8's lesson taken as written. Staff can edit category names in production through
/// <c>marketplace:categories:manage</c>, and a seeder that overwrote them on every deploy would
/// quietly undo their work every release.
/// </para>
/// <para>
/// <b>Roots first, then children by depth.</b> A child names its parent by key and the parent may
/// come from a different contributor entirely, so every seed is collected before anything is
/// created and the creation order is by nesting depth. A child whose parent nobody supplied is
/// logged and skipped rather than created at the root, because a category silently promoted to a
/// department is worse than one that is missing.
/// </para>
/// <para>
/// <b>A seeding failure is logged, never thrown</b>, and that is not defensive over-engineering —
/// it is the difference between a degraded module and a dead platform. A host that refuses to start
/// over a table only B10 needs takes every other module down with it. The integration harness relies
/// on this too: it starts the host before it creates the schema, so the first pass always finds no
/// tables.
/// </para>
/// </summary>
public sealed class MarketplaceCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<MarketplaceOptions> _options;
    private readonly ILogger<MarketplaceCatalogueSeeder> _logger;

    public MarketplaceCatalogueSeeder(
        IServiceScopeFactory scopes,
        IOptions<MarketplaceOptions> options,
        ILogger<MarketplaceCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => MarketplaceModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    /// <summary>
    /// Seeds both catalogues.
    /// <para>
    /// Public so the integration harness can re-invoke it after it has created the schema. A hosted
    /// service runs when the host starts, which in a test host is before the in-memory database
    /// exists; re-running is safe because the whole method is idempotent by contract. It is also
    /// the call a deployment whose migrations are applied by a separate job makes.
    /// </para>
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Value.SeedCatalogues)
        {
            return;
        }

        try
        {
            using var scope = _scopes.CreateScope();

            await SeedCategoriesAsync(scope.ServiceProvider, cancellationToken);
            await SeedAttributesAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown during startup. Nothing to report.
        }
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the marketplace catalogues failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedCategoriesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var contributors = services.GetServices<IListingCategoryContributor>().ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = services.GetRequiredService<IListingCategoryRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var existing = (await repository.GetAllAsync(includeInactive: true, cancellationToken))
            .ToDictionary(category => category.Key, StringComparer.Ordinal);

        // Collected across every contributor before anything is created, because a child names its
        // parent by key and the parent may come from a different contributor.
        var seeds = contributors
            .SelectMany(contributor => contributor.GetCategories())
            .Where(seed => seed is not null)
            .ToList();

        // The nodes this pass creates, tracked explicitly rather than inferred afterwards.
        // Inferring them is the shape that counts fifty categories, logs "Seeded 50", and adds none.
        var created = new List<ListingCategory>();

        // Roots first, then each level in turn, so a parent always exists by the time a child names
        // it. Two passes are enough for a tree bounded at three levels, but the loop is written
        // generally because the bound is a constant somebody may raise.
        var remaining = seeds.Where(seed => seed.ParentKey is null).ToList();
        var deferred = seeds.Where(seed => seed.ParentKey is not null).ToList();

        while (remaining.Count > 0)
        {
            foreach (var seed in remaining)
            {
                Create(seed, existing, guids, clock, created);
            }

            var next = deferred
                .Where(seed => ListingCategory.NormalizeKey(seed.ParentKey) is { } parent
                    && existing.ContainsKey(parent))
                .ToList();

            deferred = [.. deferred.Except(next)];
            remaining = next;
        }

        foreach (var orphan in deferred)
        {
            _logger.LogWarning(
                "Marketplace category '{Key}' names parent '{ParentKey}', which no contributor "
                + "supplied. It was not created.",
                orphan.Key,
                orphan.ParentKey);
        }

        if (created.Count == 0)
        {
            return;
        }

        foreach (var category in created)
        {
            await repository.AddAsync(category, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} marketplace categories.", created.Count);
    }

    private async Task SeedAttributesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var contributors = services.GetServices<IListingAttributeContributor>().ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = services.GetRequiredService<IListingAttributeDefinitionRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var existing = (await repository.GetAllAsync(includeInactive: true, cancellationToken))
            .Select(definition => (definition.Key, definition.CategoryPath))
            .ToHashSet();

        var created = new List<ListingAttributeDefinition>();

        foreach (var seed in contributors.SelectMany(
            contributor => contributor.GetAttributes()).Where(seed => seed is not null))
        {
            var path = ListingAttributeDefinition.NormalizePath(seed.CategoryPath);
            var key = Domain.Listings.ListingAttribute.NormalizeKey(seed.Key);

            if (path is null || key is null || existing.Contains((key, path)))
            {
                continue;
            }

            var definition = ListingAttributeDefinition.Create(
                ListingAttributeDefinitionId.From(guids.NewSequentialGuid()),
                key,
                seed.Label,
                path,
                seed.ValueKind,
                clock.UtcNow,
                seed.IsRequired,
                seed.IsFilterable,
                seed.Unit,
                seed.Order,
                seed.Choices is null
                    ? null
                    : [.. seed.Choices.Select(choice => (choice.Value, choice.Label))]);

            if (definition.IsFailure)
            {
                _logger.LogWarning(
                    "Marketplace attribute '{Key}' on '{Path}' is not valid and was skipped: "
                    + "{Error}.",
                    seed.Key,
                    seed.CategoryPath,
                    definition.Error.Description);

                continue;
            }

            existing.Add((key, path));
            created.Add(definition.Value);
        }

        if (created.Count == 0)
        {
            return;
        }

        foreach (var definition in created)
        {
            await repository.AddAsync(definition, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Seeded {Count} marketplace attribute definitions.", created.Count);
    }

    /// <summary>Creates one node, unless its key is already taken.</summary>
    private static void Create(
        ListingCategorySeed seed,
        Dictionary<string, ListingCategory> existing,
        IGuidGenerator guids,
        IClock clock,
        List<ListingCategory> created)
    {
        // Normalised here as well as in the aggregate, so a contributor supplying "Mobile Phones"
        // and one supplying "mobile-phones" collide on the same node rather than creating two.
        var normalized = ListingCategory.NormalizeKey(seed.Key);

        if (normalized is null || existing.ContainsKey(normalized))
        {
            return;
        }

        var parentKey = ListingCategory.NormalizeKey(seed.ParentKey);
        var parentPath = parentKey is not null && existing.TryGetValue(parentKey, out var parent)
            ? parent.Path
            : null;

        var category = ListingCategory.Create(
            ListingCategoryId.From(guids.NewSequentialGuid()),
            normalized,
            seed.Name,
            clock.UtcNow,
            parentKey,
            parentPath,
            seed.Description,
            seed.IconKey,
            seed.Order);

        if (category.IsFailure)
        {
            return;
        }

        existing[normalized] = category.Value;
        created.Add(category.Value);
    }
}
