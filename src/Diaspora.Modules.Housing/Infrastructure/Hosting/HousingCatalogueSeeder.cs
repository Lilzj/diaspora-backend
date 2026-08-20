using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing;
using Diaspora.Modules.Housing.Domain.Catalog;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Extensibility.Catalog;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Infrastructure.Hosting;

/// <summary>
/// Seeds the accommodation-type and amenity catalogues from every registered contributor.
/// <para>
/// <b>Insert-if-absent, matching on key — never an upsert.</b> An entry whose key already exists is
/// left entirely alone: not renamed, not reordered, not re-described. Staff can edit these names in
/// production, and a seeder that overwrote them on every deploy would quietly undo that work every
/// release. B8 learned it, B9 relearned it, B10 followed it, and so does this.
/// </para>
/// <para>
/// <b>A seeding failure is logged, never thrown.</b> A host that refused to start over a table only
/// B11 needs would take every other module down with it. The integration harness relies on this as
/// well: it starts the host before it creates the schema, so the first pass always finds no tables.
/// </para>
/// <para>
/// <b>A <c>BackgroundService</c> with a public swallowing <c>SeedAsync</c>, and registered in the
/// integration factory.</b> That exact shape is a platform rule the last three phases each paid for
/// separately — get it wrong and every integration test dies at host start, because the failure
/// surfaces as the whole suite being unable to create a member.
/// </para>
/// </summary>
public sealed class HousingCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<HousingOptions> _options;
    private readonly ILogger<HousingCatalogueSeeder> _logger;

    public HousingCatalogueSeeder(
        IServiceScopeFactory scopes,
        IOptions<HousingOptions> options,
        ILogger<HousingCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => HousingModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    /// <summary>
    /// Seeds both catalogues.
    /// <para>
    /// Public so the integration harness can re-invoke it after it has created the schema. Safe to
    /// call repeatedly because the whole method is idempotent by contract, and it is also the call
    /// a deployment whose migrations are applied by a separate job makes.
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

            await SeedTypesAsync(scope.ServiceProvider, cancellationToken);
            await SeedAmenitiesAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown during startup. Nothing to report.
        }
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the housing catalogues failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedTypesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var contributors = services.GetServices<IAccommodationTypeContributor>().ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = services.GetRequiredService<IAccommodationTypeRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var existing = (await repository.GetAllAsync(includeInactive: true, cancellationToken))
            .Select(type => type.Key)
            .ToHashSet(StringComparer.Ordinal);

        // Tracked explicitly rather than inferred afterwards. Inferring is the shape that counts
        // sixteen seeds, logs "Seeded 16", and adds none.
        var created = new List<AccommodationType>();

        foreach (var seed in contributors
            .SelectMany(contributor => contributor.GetAccommodationTypes())
            .Where(seed => seed is not null))
        {
            var type = AccommodationType.Create(
                AccommodationTypeId.From(guids.NewSequentialGuid()),
                seed.Key,
                seed.Name,
                clock.UtcNow,
                seed.Description,
                seed.IconKey,
                seed.IsShared,
                seed.IsTemporary,
                seed.Order);

            if (type.IsFailure || !existing.Add(type.Value.Key))
            {
                continue;
            }

            created.Add(type.Value);
        }

        if (created.Count == 0)
        {
            return;
        }

        foreach (var type in created)
        {
            await repository.AddAsync(type, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} accommodation types.", created.Count);
    }

    private async Task SeedAmenitiesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var contributors = services.GetServices<IHousingAmenityContributor>().ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = services.GetRequiredService<IHousingAmenityRepository>();
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        var existing = (await repository.GetAllAsync(includeInactive: true, cancellationToken))
            .Select(amenity => amenity.Key)
            .ToHashSet(StringComparer.Ordinal);

        var created = new List<HousingAmenity>();

        foreach (var seed in contributors
            .SelectMany(contributor => contributor.GetAmenities())
            .Where(seed => seed is not null))
        {
            var amenity = HousingAmenity.Create(
                HousingAmenityId.From(guids.NewSequentialGuid()),
                seed.Key,
                seed.Name,
                clock.UtcNow,
                seed.Group,
                seed.IconKey,
                seed.IsAccessibilityFeature,
                seed.IsFilterable,
                seed.Order);

            if (amenity.IsFailure || !existing.Add(amenity.Value.Key))
            {
                continue;
            }

            created.Add(amenity.Value);
        }

        if (created.Count == 0)
        {
            return;
        }

        foreach (var amenity in created)
        {
            await repository.AddAsync(amenity, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Seeded {Count} housing amenities.", created.Count);
    }
}
