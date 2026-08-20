using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Providers;
using Diaspora.Modules.Providers.Domain.Categories;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Extensibility.Categories;
using Diaspora.Modules.Providers.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Providers.Infrastructure.Hosting;

/// <summary>
/// Seeds the service category catalogue from every registered contributor.
/// <para>
/// <b>Idempotent, matching on slug.</b> A node whose slug already exists is left entirely alone
/// — not updated, not reordered, not re-aliased. That is deliberate: staff edit category names
/// in production, and a seeder that overwrote them on every deploy would quietly undo their work
/// every release. A contributor that genuinely needs to change an existing node does it through
/// the catalogue API, where somebody decided to.
/// </para>
/// <para>
/// Runs once at startup rather than on a schedule, because the catalogue changes when code ships
/// and at no other time.
/// </para>
/// </summary>
public sealed class ServiceCategorySeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<ProviderOptions> _options;
    private readonly ILogger<ServiceCategorySeeder> _logger;

    public ServiceCategorySeeder(
        IServiceScopeFactory scopes,
        IOptions<ProviderOptions> options,
        ILogger<ServiceCategorySeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => ProvidersModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    /// <summary>
    /// Seeds the catalogue.
    /// <para>
    /// Public so the integration harness can re-invoke it after it has created the schema. A
    /// hosted service runs when the host starts, which in a test host is before the in-memory
    /// database exists; re-running is safe because the whole method is idempotent by contract.
    /// It is also the call a deployment whose migrations are applied by a separate job would
    /// make.
    /// </para>
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.Value.SeedCategories)
        {
            return;
        }

        try
        {
            await SeedCatalogueAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown during startup. Nothing to report.
        }
#pragma warning disable CA1031 // A seeding failure must not take the host down. See below.
        catch (Exception exception)
        {
            // Logged rather than thrown. A category catalogue that failed to seed leaves the
            // module degraded — providers cannot be registered without a category — but a host
            // that refuses to start takes Identity, Trust, Location, Media and Community down
            // with it over a table B7 alone needs.
            _logger.LogError(exception, "Seeding the service category catalogue failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedCatalogueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider
            .GetServices<IServiceCategoryContributor>()
            .OrderBy(contributor => contributor.Order)
            .ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<IServiceCategoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var existing = (await repository.ListAllAsync(includeRetired: true, cancellationToken))
            .ToDictionary(category => category.Slug.Value, StringComparer.Ordinal);

        // Collected across every contributor before anything is created, because a child names
        // its parent by slug and the parent may come from a different contributor.
        var seeds = contributors
            .SelectMany(contributor => contributor.GetCategories())
            .ToList();

        // The nodes this pass creates, tracked explicitly rather than inferred afterwards.
        // Inferring them from a timestamp or an unset field is the shape that counts fifty-seven
        // categories, logs "Seeded 57", and adds none of them — which is exactly what an earlier
        // draft of this method did.
        var created = new List<ServiceCategory>();

        // Two passes: roots first, then children, so a parent always exists by the time a child
        // references it. Deeper trees would need a topological sort; two levels of real depth
        // plus one of headroom does not.
        foreach (var seed in seeds.Where(seed => seed.ParentSlug is null))
        {
            Create(seed, existing, null, guids, clock, created);
        }

        foreach (var seed in seeds.Where(seed => seed.ParentSlug is not null))
        {
            var parent = existing.GetValueOrDefault(seed.ParentSlug!);

            if (parent is null)
            {
                _logger.LogWarning(
                    "Category '{Slug}' names parent '{ParentSlug}', which no contributor supplied.",
                    seed.Slug,
                    seed.ParentSlug);

                continue;
            }

            Create(seed, existing, parent, guids, clock, created);
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

        _logger.LogInformation("Seeded {Count} service categories.", created.Count);
    }

    /// <summary>Creates one node, unless its slug is already taken.</summary>
    private static void Create(
        ServiceCategorySeed seed,
        Dictionary<string, ServiceCategory> existing,
        ServiceCategory? parent,
        IGuidGenerator guids,
        IClock clock,
        List<ServiceCategory> created)
    {
        if (existing.ContainsKey(seed.Slug))
        {
            return;
        }

        var slug = CategorySlug.Create(seed.Slug);

        if (slug.IsFailure)
        {
            return;
        }

        var id = ServiceCategoryId.From(guids.NewSequentialGuid());
        var now = clock.UtcNow;

        var category = parent is null
            ? ServiceCategory.CreateRoot(
                id, slug.Value, seed.Name, now, seed.Icon, seed.Description, seed.SortOrder)
            : ServiceCategory.CreateChild(
                id, parent, slug.Value, seed.Name, now, seed.Icon, seed.Description, seed.SortOrder);

        if (category.IsFailure)
        {
            return;
        }

        foreach (var alias in seed.Aliases)
        {
            category.Value.AddAlias(
                ServiceCategoryAliasId.From(guids.NewGuid()), alias, "seed");
        }

        foreach (var keyword in seed.Keywords)
        {
            category.Value.AddKeyword(
                ServiceCategoryKeywordId.From(guids.NewGuid()), keyword);
        }

        foreach (var (languageCode, name) in seed.Translations)
        {
            category.Value.SetTranslation(
                ServiceCategoryTranslationId.From(guids.NewGuid()), languageCode, name);
        }

        // The node's own slug is registered as an alias too, so a client sending the slug
        // resolves through the same index as one sending a legacy display name.
        category.Value.AddAlias(
            ServiceCategoryAliasId.From(guids.NewGuid()), seed.Name, "seed");

        existing[seed.Slug] = category.Value;
        created.Add(category.Value);
    }
}
