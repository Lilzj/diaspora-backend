using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Domain.Categories;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Extensibility.Categories;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Infrastructure.Hosting;

/// <summary>
/// Seeds the guide category catalogue from every registered contributor.
/// <para>
/// <b>Idempotent, matching on key.</b> A category whose key already exists is left entirely alone
/// — not renamed, not reordered, not re-described. That is deliberate: staff edit category names
/// in production, and a seeder that overwrote them on every deploy would quietly undo their work
/// every release. A contributor that genuinely needs to change an existing node does it through
/// the catalogue API, where somebody decided to.
/// </para>
/// <para>
/// Runs once at startup rather than on a schedule, because the catalogue changes when code ships
/// and at no other time.
/// </para>
/// </summary>
public sealed class GuideCategorySeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<GuideOptions> _options;
    private readonly ILogger<GuideCategorySeeder> _logger;

    public GuideCategorySeeder(
        IServiceScopeFactory scopes,
        IOptions<GuideOptions> options,
        ILogger<GuideCategorySeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => GuidesModuleMetadata.Name;

    /// <summary>B8's guide categories are the first of the module's three catalogues, and the only one a
    /// guide is refused without.</summary>
    public int Order => 0;

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
            // Logged rather than thrown. A catalogue that failed to seed leaves the module
            // degraded — a guide cannot be written without a category — but a host that refuses
            // to start takes Identity, Trust, Location, Media, Community and Providers down with
            // it over a table B8 alone needs.
            _logger.LogError(exception, "Seeding the guide category catalogue failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedCatalogueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider
            .GetServices<IGuideCategoryContributor>()
            .OrderBy(contributor => contributor.Order)
            .ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<IGuideCategoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var existing = new HashSet<string>(
            (await repository.ListAsync(includeInactive: true, cancellationToken))
                .Select(category => category.Key),
            StringComparer.Ordinal);

        // Collected across every contributor before anything is created, because a child names
        // its parent by key and the parent may come from a different contributor.
        var seeds = contributors
            .SelectMany(contributor => contributor.GetCategories())
            .ToList();

        // The nodes this pass creates, tracked explicitly rather than inferred afterwards.
        // Inferring them from a timestamp or an unset field is the shape that counts twenty-one
        // categories, logs "Seeded 21", and adds none of them.
        var created = new List<GuideCategory>();

        // Roots first, then children, so a parent always exists by the time a child names it.
        foreach (var seed in seeds.Where(seed => seed.ParentKey is null))
        {
            Create(seed, existing, guids, clock, created);
        }

        foreach (var seed in seeds.Where(seed => seed.ParentKey is not null))
        {
            if (!existing.Contains(seed.ParentKey!))
            {
                _logger.LogWarning(
                    "Guide category '{Key}' names parent '{ParentKey}', which no contributor supplied.",
                    seed.Key,
                    seed.ParentKey);

                continue;
            }

            Create(seed, existing, guids, clock, created);
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

        _logger.LogInformation("Seeded {Count} guide categories.", created.Count);
    }

    /// <summary>Creates one node, unless its key is already taken.</summary>
    private static void Create(
        GuideCategorySeed seed,
        HashSet<string> existing,
        IGuidGenerator guids,
        IClock clock,
        List<GuideCategory> created)
    {
        // The key is normalised by the aggregate, so a contributor supplying "Immigration" and
        // one supplying "immigration" collide on the same node rather than creating two.
        var normalized = seed.Key?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalized) || existing.Contains(normalized))
        {
            return;
        }

        var category = GuideCategory.Create(
            GuideCategoryId.From(guids.NewSequentialGuid()),
            normalized,
            seed.Name,
            clock.UtcNow,
            seed.Description,
            seed.Icon,
            seed.Order,
            seed.ParentKey);

        if (category.IsFailure)
        {
            return;
        }

        existing.Add(normalized);
        created.Add(category.Value);
    }
}
