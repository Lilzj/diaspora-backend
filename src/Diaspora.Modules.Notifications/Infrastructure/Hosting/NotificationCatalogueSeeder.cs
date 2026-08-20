using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Identifiers;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Extensibility.Catalog;
using Diaspora.Modules.Notifications.Extensibility.Routing;
using Diaspora.Modules.Notifications.Extensibility.Templates;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Infrastructure.Hosting;

/// <summary>
/// Seeds the categories, the routing rules and the templates every contributor declares.
/// <para>
/// <b>A hosted service with a public, swallowing <see cref="SeedAsync"/>, and both halves of that
/// shape are a contract this platform has paid for.</b> The service runs at host start, before
/// migrations have necessarily been applied — so its first attempt may find no tables, and throwing
/// there takes the whole host down. The integration suite creates its schema after the host starts
/// and then calls <see cref="SeedAsync"/> again, which is why the method is public and why every
/// seeder must be idempotent by contract.
/// </para>
/// <para>
/// <b>Seeding never overwrites an operator's edits.</b> A contributor's declaration creates what is
/// missing and refreshes what it still owns; a template an operator has since versioned through the
/// authoring API is left alone. Contributions seed, they do not enforce — otherwise every deployment
/// would silently revert a wording fix somebody made at three in the morning.
/// </para>
/// </summary>
public sealed class NotificationCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificationCatalogueSeeder> _logger;

    public NotificationCatalogueSeeder(
        IServiceScopeFactory scopes, ILogger<NotificationCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => NotificationsModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => SeedAsync(stoppingToken);

    /// <summary>
    /// Brings the catalogue up to date. Idempotent, and swallows everything.
    /// <para>
    /// Public because a host that applies migrations separately — which the integration suite does —
    /// has to be able to call it again once the schema exists.
    /// </para>
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var services = scope.ServiceProvider;

            await SeedCategoriesAsync(services, cancellationToken);
            await SeedTemplatesAsync(services, cancellationToken);
            await SeedRulesAsync(services, cancellationToken);

            await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception exception)
#pragma warning restore CA1031
        {
            // The schema may not exist yet. Logged at debug rather than error because on a normal
            // start with a separate migration job this is expected and self-correcting.
            _logger.LogDebug(
                exception,
                "The notification catalogue could not be seeded yet. It will be retried when the "
                + "schema is available.");
        }
    }

    private static async Task SeedCategoriesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<INotificationCategoryRepository>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        foreach (var contributor in services.GetServices<INotificationCategoryContributor>())
        {
            foreach (var seed in contributor.GetCategories())
            {
                var existing = await repository.FindByKeyAsync(seed.Key, cancellationToken);

                if (existing is not null)
                {
                    existing.Refresh(
                        seed.DisplayName,
                        seed.Description,
                        seed.DefaultPriority,
                        seed.IsMandatory,
                        seed.DigestByDefault,
                        seed.Position);

                    repository.Update(existing);

                    continue;
                }

                var created = NotificationCategory.Create(
                    NotificationCategoryId.From(guids.NewSequentialGuid()),
                    seed.Key,
                    seed.DisplayName,
                    contributor.Module,
                    seed.DefaultPriority,
                    clock.UtcNow,
                    seed.Description,
                    seed.IsMandatory,
                    seed.DigestByDefault,
                    seed.Position);

                if (created.IsSuccess)
                {
                    await repository.AddAsync(created.Value, cancellationToken);
                }
            }
        }
    }

    /// <summary>
    /// Creates templates that do not exist, and leaves alone every one that does.
    /// <para>
    /// <b>Existing templates are never refreshed from a seed.</b> That is the difference between a
    /// template and a category: a category's label is reference data the module owns, whereas a
    /// template's wording is something an operator may have deliberately changed. Overwriting it on
    /// every deployment would revert a fix somebody made during an incident.
    /// </para>
    /// </summary>
    private static async Task SeedTemplatesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<INotificationTemplateRepository>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();
        var assembler = services.GetRequiredService<NotificationTemplateSeedAssembler>();

        foreach (var contributor in services.GetServices<INotificationTemplateContributor>())
        {
            foreach (var seed in contributor.GetTemplates())
            {
                if (await repository.FindPublishedAsync(seed.Key, cancellationToken) is not null)
                {
                    continue;
                }

                if (await repository.FindDraftAsync(seed.Key, cancellationToken) is not null)
                {
                    continue;
                }

                var template = assembler.Build(seed, guids, clock.UtcNow);

                if (template is null)
                {
                    continue;
                }

                await repository.AddAsync(template, cancellationToken);
            }
        }
    }

    private static async Task SeedRulesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<INotificationRuleRepository>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        foreach (var contributor in services.GetServices<INotificationRuleContributor>())
        {
            foreach (var seed in contributor.GetRules())
            {
                var existing = await repository.FindByKeyAsync(seed.Key, cancellationToken);

                if (existing is not null)
                {
                    existing.Refresh(
                        seed.TemplateKey,
                        seed.CategoryKey,
                        seed.Priority,
                        seed.Policy,
                        seed.Channels,
                        seed.Offset,
                        seed.RetentionPeriod,
                        seed.DeduplicationWindow);

                    repository.Update(existing);

                    continue;
                }

                var created = NotificationRule.Create(
                    NotificationRuleId.From(guids.NewSequentialGuid()),
                    seed.Key,
                    seed.EventName,
                    seed.Audience,
                    seed.TemplateKey,
                    seed.CategoryKey,
                    seed.Priority,
                    seed.Policy,
                    seed.Channels,
                    contributor.Module,
                    clock.UtcNow,
                    seed.Offset,
                    seed.RetentionPeriod,
                    seed.DeduplicationWindow);

                if (created.IsSuccess)
                {
                    await repository.AddAsync(created.Value, cancellationToken);
                }
            }
        }
    }
}
