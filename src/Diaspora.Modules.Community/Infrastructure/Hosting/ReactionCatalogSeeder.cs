using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community;
using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Modules.Community.Extensibility.Reactions;
using Diaspora.Modules.Community.Options;
using Diaspora.Platform.Modules.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Community.Infrastructure.Hosting;

/// <summary>
/// Seeds the reaction catalogue from every registered contributor.
/// <para>
/// Runs once at startup and is idempotent by key: a definition that already exists is left
/// alone, including its enabled state and its tuned weight. A deployment that disabled a
/// built-in reaction or retuned its weight must not have that undone by a restart.
/// </para>
/// <para>
/// Public because the integration test factory creates its schema after the host starts, which
/// is the same reason B4's catalogue seeder exposes its own <c>SeedAsync</c>.
/// </para>
/// </summary>
public sealed class ReactionCatalogSeeder : IHostedService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly CommunityOptions _options;
    private readonly ILogger<ReactionCatalogSeeder> _logger;

    public ReactionCatalogSeeder(
        IServiceScopeFactory scopeFactory,
        IOptions<CommunityOptions> options,
        ILogger<ReactionCatalogSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options?.Value ?? new CommunityOptions();
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => CommunityModuleMetadata.Name;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.SeedReactionCatalog)
        {
            return;
        }

        try
        {
            await SeedAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A seed failure must not stop the host from starting.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Seeding the reaction catalogue failed. The module starts with whatever is stored.");
        }
#pragma warning restore CA1031
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IReactionDefinitionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var contributors = scope.ServiceProvider
            .GetServices<IReactionCatalogContributor>()
            .ToList();

        var existing = (await repository.ListAsync(true, cancellationToken))
            .Select(definition => definition.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;

        foreach (var definition in contributors.SelectMany(c => c.GetDefinitions()))
        {
            if (!existing.Add(definition.Key))
            {
                continue;
            }

            await repository.AddAsync(definition, cancellationToken);
            added++;
        }

        if (added > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Seeded {Count} reaction definitions.", added);
        }
    }
}
