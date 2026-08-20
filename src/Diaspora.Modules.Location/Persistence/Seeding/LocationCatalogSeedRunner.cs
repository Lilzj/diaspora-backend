using Diaspora.Modules.Location;
using Diaspora.Modules.Location.Extensibility;
using Diaspora.Platform.Modules.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Location.Persistence.Seeding;

/// <summary>
/// Runs every registered catalogue seeder once, at startup.
/// <para>
/// A hosted service the module registers itself, which is how it seeds without an edit to
/// <c>Program.cs</c>. It resolves its own scope because seeders need scoped repositories and a
/// hosted service is a singleton.
/// </para>
/// <para>
/// <b>It never fails the host.</b> A seeding error is logged and swallowed, because the
/// alternative is an application that will not start because a reference dataset could not be
/// written — and a platform whose sign-in is down because the country list was already there
/// is a worse outcome than one whose country list is incomplete. The log line is the signal;
/// the empty catalogue is visible immediately in any location screen.
/// </para>
/// </summary>
public sealed class LocationCatalogSeedRunner : IHostedService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<LocationSpatialOptions> _options;
    private readonly ILogger<LocationCatalogSeedRunner> _logger;

    public LocationCatalogSeedRunner(
        IServiceScopeFactory scopeFactory,
        IOptions<LocationSpatialOptions> options,
        ILogger<LocationCatalogSeedRunner> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => LocationModuleMetadata.Name;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Value.SeedCatalogOnStartup)
        {
            _logger.LogInformation(
                "Location catalogue seeding is disabled by configuration.");
            return;
        }

        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(
                exception,
                "Location catalogue seeding failed. The platform will start with whatever " +
                "geography is already present.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Runs every seeder once. Public so a host that creates its schema after start-up can
    /// invoke it when the database is actually ready.
    /// <para>
    /// That is not only a test concern. A deployment whose migrations are applied by a
    /// separate job — the normal arrangement once more than one instance runs — starts the
    /// application before the schema exists, and a seeder that gave up permanently on the
    /// first boot would leave the catalogue empty until someone noticed. Re-invocation is safe
    /// because every seeder is idempotent.
    /// </para>
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        var seeders = scope.ServiceProvider
            .GetServices<ILocationCatalogSeeder>()
            .OrderBy(seeder => seeder.Order)
            .ThenBy(seeder => seeder.Name, StringComparer.Ordinal)
            .ToList();

        if (seeders.Count == 0)
        {
            return;
        }

        var writer = scope.ServiceProvider.GetRequiredService<LocationCatalogWriter>();

        foreach (var seeder in seeders)
        {
            await seeder.SeedAsync(writer, cancellationToken);
            _logger.LogInformation("Location seeder '{Seeder}' completed.", seeder.Name);
        }

        // One save for the whole chain, so a failure halfway does not leave countries without
        // the geography that hangs off them.
        var written = await writer.CommitAsync(cancellationToken);

        _logger.LogInformation(
            "Location catalogue seeding wrote {Rows} rows across {Seeders} seeders.",
            written, seeders.Count);
    }
}
