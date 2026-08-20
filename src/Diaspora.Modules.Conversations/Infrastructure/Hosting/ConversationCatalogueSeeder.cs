using Diaspora.Modules.Conversations.Domain.Catalog;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Domain.Abstractions;
using Diaspora.Platform.Modules.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Conversations.Infrastructure.Hosting;

/// <summary>
/// Puts every contributed conversation type into the catalogue at startup.
/// <para>
/// <b>A <see cref="BackgroundService"/> with a public, swallowing <see cref="SeedAsync"/>, and every
/// part of that shape is required rather than stylistic.</b> The platform's seeder discovery calls
/// <c>SeedAsync</c> directly in the integration host; a seeder that only ran from
/// <c>ExecuteAsync</c> would never run there. And it must swallow: a seeder that threw would take
/// the host down at boot, and the platform has already paid once for three hundred integration tests
/// dying because one seeder found a dependency that was not registered.
/// </para>
/// <para>
/// <b>Idempotent by re-applying rather than by skipping.</b> A contributor's seed is its current
/// statement of what a type means — if <c>RequiresAcceptance</c> changed, the catalogue must change
/// with it. The key is never touched, because conversations already reference it.
/// </para>
/// </summary>
public sealed class ConversationCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ConversationCatalogueSeeder> _logger;

    public ConversationCatalogueSeeder(
        IServiceScopeFactory scopes, ILogger<ConversationCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public string Module => ConversationsModuleMetadata.Name;

    public int Order => 10;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => SeedAsync(stoppingToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            var repository = scope.ServiceProvider
                .GetRequiredService<IConversationContextTypeRepository>();

            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var contributors = scope.ServiceProvider
                .GetServices<IConversationContextTypeContributor>()
                .ToList();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var written = 0;

            foreach (var seed in contributors.SelectMany(contributor => contributor.GetContextTypes()))
            {
                if (string.IsNullOrWhiteSpace(seed.Key) || !seen.Add(seed.Key))
                {
                    // A duplicate key from two contributors is a collision worth naming: the second
                    // would silently overwrite the first, and the symptom would be a vertical whose
                    // conversations acquire somebody else's acceptance rule.
                    _logger.LogWarning(
                        "Two contributors claim the conversation context type '{Key}'. The first "
                        + "wins; the second is ignored.",
                        seed.Key);

                    continue;
                }

                var existing = await repository.GetAsync(seed.Key, cancellationToken);

                if (existing is null)
                {
                    repository.Add(ConversationContextType.Create(
                        seed.Key,
                        seed.DisplayName,
                        seed.Description,
                        seed.OwningModule,
                        seed.RequiresAcceptance,
                        seed.AllowsAttachments,
                        seed.IsSystemManaged,
                        seed.Position));
                }
                else
                {
                    existing.Apply(
                        seed.DisplayName,
                        seed.Description,
                        seed.OwningModule,
                        seed.RequiresAcceptance,
                        seed.AllowsAttachments,
                        seed.IsSystemManaged,
                        seed.Position);

                    repository.Update(existing);
                }

                written++;
            }

            if (written > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation(
                "Seeded {Count} conversation context types from {Contributors} contributors.",
                written,
                contributors.Count);
        }
        catch (OperationCanceledException)
        {
            // Shutdown. Not a fault.
        }
#pragma warning disable CA1031 // A seeder that throws takes the host down at boot. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Seeding the conversation context type catalogue failed. Conversations will refuse "
                + "unknown types until this is resolved.");
        }
#pragma warning restore CA1031
    }
}
