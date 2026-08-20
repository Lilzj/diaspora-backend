using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs;
using Diaspora.Modules.Jobs.Domain.Catalog;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Extensibility.Categories;
using Diaspora.Modules.Jobs.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Jobs.Infrastructure.Hosting;

/// <summary>
/// Seeds the employment-domain catalogue from every registered contributor.
/// <para>
/// <b>Insert-if-absent, matching on key — never an upsert.</b> A category whose key already
/// exists is left entirely alone: not renamed, not reordered, not re-described. That is
/// deliberate and it is B8's lesson taken as written. Staff can edit category names in
/// production through <c>jobs:categories:manage</c>, and a seeder that overwrote them on every
/// deploy would quietly undo their work every release. A contributor that genuinely needs to
/// change an existing node does it through the catalogue API, where somebody decided to.
/// </para>
/// <para>
/// Runs once at startup rather than on a schedule, because the catalogue changes when code ships
/// and at no other time.
/// </para>
/// <para>
/// <b>A seeding failure is logged, never thrown</b>, and that is not defensive
/// over-engineering — it is the difference between a degraded module and a dead platform. A
/// host that refuses to start over a table only B9 needs takes Identity, Trust, Location, Media,
/// Community, Providers and Guides down with it. The integration harness relies on this too: it
/// starts the host before it creates the schema, so the first pass always finds no tables.
/// </para>
/// </summary>
public sealed class JobCategorySeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<JobOptions> _options;
    private readonly ILogger<JobCategorySeeder> _logger;

    public JobCategorySeeder(
        IServiceScopeFactory scopes,
        IOptions<JobOptions> options,
        ILogger<JobCategorySeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => JobsModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    /// <summary>
    /// Seeds the catalogue.
    /// <para>
    /// Public so the integration harness can re-invoke it after it has created the schema. A
    /// hosted service runs when the host starts, which in a test host is before the in-memory
    /// database exists; re-running is safe because the whole method is idempotent by contract.
    /// It is also the call a deployment whose migrations are applied by a separate job makes.
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
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the job category catalogue failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedCatalogueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider
            .GetServices<IJobCategoryContributor>()
            .ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<IJobCategoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        var existing = new HashSet<string>(
            (await repository.GetAllAsync(includeInactive: true, cancellationToken))
                .Select(category => category.Key),
            StringComparer.Ordinal);

        // Collected across every contributor before anything is created, because a child names
        // its parent by key and the parent may come from a different contributor.
        var seeds = contributors
            .SelectMany(contributor => contributor.GetCategories())
            .Where(seed => seed is not null)
            .ToList();

        // The nodes this pass creates, tracked explicitly rather than inferred afterwards.
        // Inferring them is the shape that counts fourteen categories, logs "Seeded 14", and
        // adds none of them.
        var created = new List<JobCategory>();

        // Roots first, then children, so a parent always exists by the time a child names it.
        foreach (var seed in seeds.Where(seed => seed.ParentKey is null))
        {
            Create(seed, existing, guids, clock, created);
        }

        foreach (var seed in seeds.Where(seed => seed.ParentKey is not null))
        {
            var parent = JobCategory.NormalizeKey(seed.ParentKey);

            if (parent is null || !existing.Contains(parent))
            {
                _logger.LogWarning(
                    "Job category '{Key}' names parent '{ParentKey}', which no contributor supplied.",
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

        _logger.LogInformation("Seeded {Count} job categories.", created.Count);
    }

    /// <summary>Creates one node, unless its key is already taken.</summary>
    private static void Create(
        JobCategorySeed seed,
        HashSet<string> existing,
        IGuidGenerator guids,
        IClock clock,
        List<JobCategory> created)
    {
        // Normalised here as well as in the aggregate, so a contributor supplying "Healthcare"
        // and one supplying "healthcare" collide on the same node rather than creating two.
        var normalized = JobCategory.NormalizeKey(seed.Key);

        if (normalized is null || existing.Contains(normalized))
        {
            return;
        }

        var category = JobCategory.Create(
            JobCategoryId.From(guids.NewSequentialGuid()),
            normalized,
            seed.Name,
            clock.UtcNow,
            seed.ParentKey,
            seed.Description,
            seed.IconKey,
            seed.Order);

        if (category.IsFailure)
        {
            return;
        }

        existing.Add(normalized);
        created.Add(category.Value);
    }
}
