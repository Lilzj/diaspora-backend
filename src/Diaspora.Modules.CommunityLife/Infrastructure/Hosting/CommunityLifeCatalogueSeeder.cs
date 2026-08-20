using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife;
using Diaspora.Modules.CommunityLife.Domain.Catalog;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Extensibility.Catalog;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Hosting;

/// <summary>
/// Puts the contributed organization types and event categories into the database at startup.
/// <para>
/// <b>A <see cref="BackgroundService"/> with a public <see cref="SeedAsync"/>, and both halves of
/// that matter.</b> As a hosted service it runs after the host is up, so a slow or failing seed does
/// not stop the application from serving; as a public method the integration test factory can await
/// it deterministically instead of racing the host. A seeder that only ran as a hosted service would
/// make every integration test flaky.
/// </para>
/// <para>
/// <b>Every failure is swallowed and logged.</b> A catalogue that did not seed leaves a deployment
/// with empty pickers, which somebody notices and fixes; an exception out of a hosted service takes
/// the host down at startup, which is the same outage for every other module too.
/// </para>
/// <para>
/// <b>Existing keys are never overwritten.</b> A deployment that renamed "Place of worship" or
/// retired a category means it, and a seeder that restated the contributor's values on every boot
/// would undo that silently once a night.
/// </para>
/// </summary>
public sealed class CommunityLifeCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<CommunityLifeCatalogueSeeder> _logger;

    public CommunityLifeCatalogueSeeder(
        IServiceScopeFactory scopes, ILogger<CommunityLifeCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => CommunityLifeModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            await SeedTypesAsync(scope.ServiceProvider, cancellationToken);
            await SeedCategoriesAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown during startup. Nothing to report.
        }
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the Community Life catalogues failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedTypesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var contributors = services.GetServices<IOrganizationTypeContributor>().ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = services.GetRequiredService<IOrganizationTypeRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var existing = (await repository.ListAsync(includeRetired: true, cancellationToken))
            .Select(type => type.Key)
            .ToHashSet(StringComparer.Ordinal);

        // Tracked explicitly rather than inferred from the seed count afterwards. Inferring is the
        // shape that counts fourteen seeds, logs "Seeded 14", and adds none.
        var created = 0;

        foreach (var seed in contributors
            .SelectMany(contributor => contributor.GetOrganizationTypes())
            .Where(seed => seed is not null))
        {
            var type = OrganizationType.Create(
                OrganizationTypeId.From(guids.NewSequentialGuid()),
                seed.Key,
                seed.Name,
                clock.UtcNow,
                seed.Description,
                seed.IconName,
                seed.GroupName,
                seed.Order,
                seed.EncouragesVerification);

            // Checked against the normalized key the aggregate produced, not the raw one from the
            // descriptor: a contributor writing "Place Of Worship" must not seed a second row
            // beside the "place-of-worship" already there.
            if (!existing.Add(type.Key))
            {
                continue;
            }

            await repository.AddAsync(type, cancellationToken);
            created++;
        }

        if (created == 0)
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} Community Life organization types.", created);
    }

    private async Task SeedCategoriesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var contributors = services.GetServices<IEventCategoryContributor>().ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = services.GetRequiredService<IEventCategoryRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var existing = (await repository.ListAsync(includeRetired: true, cancellationToken))
            .Select(category => category.Key)
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;

        foreach (var seed in contributors
            .SelectMany(contributor => contributor.GetEventCategories())
            .Where(seed => seed is not null))
        {
            var category = EventCategory.Create(
                EventCategoryId.From(guids.NewSequentialGuid()),
                seed.Key,
                seed.Name,
                clock.UtcNow,
                seed.Description,
                seed.IconName,
                seed.GroupName,
                seed.Facets,
                seed.Order,
                seed.DefaultsToRsvp);

            if (!existing.Add(category.Key))
            {
                continue;
            }

            await repository.AddAsync(category, cancellationToken);
            created++;
        }

        if (created == 0)
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} Community Life event categories.", created);
    }
}
