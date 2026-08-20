using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Infrastructure.Hosting;

/// <summary>
/// Seeds the notice category catalogue from every registered contributor.
/// <para>
/// <b>Idempotent, matching on key.</b> A row whose key already exists is left entirely alone — not
/// renamed, not reordered, not re-severitied, and its translations untouched. That is deliberate:
/// staff edit these in production, and a seeder that overwrote them on every deploy would quietly
/// undo their work every release. Changing a shipped category is done through the catalogue API,
/// where somebody decided to.
/// </para>
/// <para>
/// <b>A failure here degrades the module rather than taking the host down.</b> Without a catalogue no
/// notice can be filed, which is bad — and a host that refuses to start takes Identity, Trust,
/// Location, Media, Community, Providers and every vertical down with it over a table B8.2 alone
/// needs, which is worse. B8's and B8.1's seeders made the same call for the same reason.
/// </para>
/// </summary>
public sealed class NoticeCategorySeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IOptions<IntelligenceOptions> _options;
    private readonly ILogger<NoticeCategorySeeder> _logger;

    public NoticeCategorySeeder(
        IServiceScopeFactory scopes,
        IOptions<IntelligenceOptions> options,
        ILogger<NoticeCategorySeeder> logger)
    {
        _scopes = scopes;
        _options = options;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => GuidesModuleMetadata.Name;

    /// <summary>B8.2's notice categories are last of the module's three, and depend on neither — ordered
    /// explicitly so the sequence is a decision rather than an alphabetical accident.</summary>
    public int Order => 20;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        SeedAsync(stoppingToken);

    /// <summary>
    /// Seeds the catalogue.
    /// <para>
    /// <b>Public, and it has to be.</b> A hosted service runs when the host starts, which in the
    /// integration harness is before the in-memory database exists — so its first attempt finds no
    /// tables and logs a failure. The harness re-invokes this afterwards, and every module that ships
    /// reference data does the same. A seeder that is not re-invokable is a suite where every test
    /// dies at host start; the platform has that failure on record.
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
            _logger.LogError(exception, "Seeding the notice category catalogue failed.");
        }
#pragma warning restore CA1031
    }

    private async Task SeedCatalogueAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();

        var contributors = scope.ServiceProvider
            .GetServices<IOfficialNoticeCategoryContributor>()
            .OrderBy(contributor => contributor.Order)
            .ToList();

        if (contributors.Count == 0)
        {
            return;
        }

        var repository = scope.ServiceProvider.GetRequiredService<INoticeCategoryRepository>();
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
        // timestamp is the shape that counts twenty rows, logs "Seeded 20", and adds none of them.
        var created = new List<NoticeCategory>();

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

        _logger.LogInformation("Seeded {Count} notice categories.", created.Count);
    }

    /// <summary>Creates one row, unless its key is already taken.</summary>
    private static NoticeCategory? Create(
        NoticeCategorySeed seed,
        HashSet<string> existing,
        IGuidGenerator guids,
        IClock clock)
    {
        // Normalised before the check, so a contributor supplying "Visa-Advisories" and one
        // supplying "visa-advisories" collide on the same row rather than creating two.
        var normalized = IntelligenceNormalization.Key(seed.Key);

        if (normalized is null || !existing.Add(normalized))
        {
            return null;
        }

        var created = NoticeCategory.Create(
            NoticeCategoryId.From(guids.NewSequentialGuid()),
            normalized,
            seed.Name,
            clock.UtcNow,
            seed.Description,
            seed.Icon,
            seed.Order,
            seed.DefaultSeverity,
            seed.AlertFamily);

        if (created.IsFailure)
        {
            return null;
        }

        var category = created.Value;

        if (seed.Localizations.Count > 0)
        {
            // Through the aggregate rather than by construction, so a seed naming an unusable locale
            // is refused by exactly the rules a curator's edit would be. A refusal leaves the row
            // created and the translations empty, which is recoverable through the API.
            category.SetLocalizations(
                [.. seed.Localizations.Select(
                    localization =>
                        ((string?)localization.Key, (string?)localization.Value, (string?)null))],
                changedBy: null);
        }

        return category;
    }
}
