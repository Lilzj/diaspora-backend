using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Knowledge;
using Diaspora.Modules.Guides.Extensibility.Knowledge;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Infrastructure.Hosting;

/// <summary>
/// Seeds the Knowledge Directory's navigation catalogue from every registered contributor.
/// <para>
/// <b>Idempotent, matching on key.</b> A row whose key already exists is left entirely alone — not
/// renamed, not reordered, not re-described, and its translations untouched. That is deliberate:
/// staff edit navigation labels in production, and a seeder that overwrote them on every deploy
/// would quietly undo their work every release. A contributor that genuinely needs to change an
/// existing row does it through the catalogue API, where somebody decided to.
/// </para>
/// <para>
/// <b>A failure here degrades the module rather than taking the host down.</b> Without a catalogue
/// there is no navigation, which is bad — and a host that refuses to start takes Identity, Trust,
/// Location, Media, Community, Providers and every vertical down with it over a table B8.1 alone
/// needs, which is worse. B8's own category seeder made the same call for the same reason.
/// </para>
/// </summary>
public sealed class KnowledgeCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<KnowledgeOptions> _options;
    private readonly ILogger<KnowledgeCatalogueSeeder> _logger;

    public KnowledgeCatalogueSeeder(
        IServiceScopeFactory scopes,
        IOptions<KnowledgeOptions> options,
        ILogger<KnowledgeCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => GuidesModuleMetadata.Name;

    /// <summary>B8.1's navigation catalogue arranges country directories over the categories above.</summary>
    public int Order => 10;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    /// <summary>
    /// Seeds the catalogue.
    /// <para>
    /// <b>Public, and it has to be.</b> A hosted service runs when the host starts, which in the
    /// integration harness is before the in-memory database exists — so its first attempt finds no
    /// tables and logs a failure. The harness re-invokes this afterwards, and every module that
    /// ships reference data does the same. A seeder that is not re-invokable is a suite where every
    /// test dies at host start; the platform has that failure on record.
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
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the class summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the knowledge navigation catalogue failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedCatalogueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider
            .GetServices<IKnowledgeCategoryContributor>()
            .OrderBy(contributor => contributor.Order)
            .ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<IKnowledgeCategoryRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var guids = scope.ServiceProvider.GetRequiredService<IGuidGenerator>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        // Archived rows included: an archived key is still taken, the unique index would refuse a
        // second, and re-creating it would strand the archived row beyond any restore.
        var existing = new HashSet<string>(
            (await repository.ListAsync(includeArchived: true, cancellationToken))
                .Select(category => category.Key),
            StringComparer.Ordinal);

        // Tracked explicitly rather than inferred afterwards. Inferring the created set from a
        // timestamp or an unset field is the shape that counts twenty-four rows, logs "Seeded 24",
        // and adds none of them — a mistake B8's seeder documents having nearly made.
        var created = new List<KnowledgeCategory>();

        foreach (var seed in contributors.SelectMany(contributor => contributor.GetCategories()))
        {
            var category = Create(seed, existing, guids, clock);

            if (category is not null)
            {
                created.Add(category);
            }
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

        _logger.LogInformation("Seeded {Count} knowledge categories.", created.Count);
    }

    /// <summary>Creates one row, unless its key is already taken.</summary>
    private static KnowledgeCategory? Create(
        KnowledgeCategorySeed seed,
        HashSet<string> existing,
        IGuidGenerator guids,
        IClock clock)
    {
        // Normalised before the check, so a contributor supplying "Immigration" and one supplying
        // "immigration" collide on the same row rather than creating two.
        var normalized = KnowledgeNormalization.Key(seed.Key);

        if (normalized is null || !existing.Add(normalized))
        {
            return null;
        }

        var created = KnowledgeCategory.Create(
            KnowledgeCategoryId.From(guids.NewSequentialGuid()),
            normalized,
            seed.Name,
            clock.UtcNow,
            seed.Description,
            seed.Icon,
            seed.Order);

        if (created.IsFailure)
        {
            return null;
        }

        var category = created.Value;

        // Sources, provider categories and translations are set through the aggregate rather than
        // by construction, so a seed that names an unusable key is refused by the same rules a
        // curator's edit would be. A refusal leaves the row created and the collection empty, which
        // is recoverable through the API; refusing the row entirely would leave a country directory
        // pointing at navigation that does not exist.
        category.SetSources(seed.SourceCategoryKeys, changedBy: null);
        category.SetProviderCategories(seed.ProviderCategoryKeys, changedBy: null);

        if (seed.Localizations.Count > 0)
        {
            category.SetLocalizations(
                [.. seed.Localizations.Select(
                    localization => ((string?)localization.Key, (string?)localization.Value, (string?)null))],
                changedBy: null);
        }

        return category;
    }
}
