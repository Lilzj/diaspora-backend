using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Recommendations;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.Modules.Recommendations.Domain.Policies;
using Diaspora.Modules.Recommendations.Domain.Strategies;
using Diaspora.Modules.Recommendations.Extensibility.Catalog;
using Diaspora.Modules.Recommendations.Extensibility.Ranking;
using Diaspora.Modules.Recommendations.Infrastructure.Policies;
using Diaspora.Modules.Recommendations.Observability;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Recommendations.Infrastructure.Hosting;

/// <summary>
/// Seeds the ranking strategies and recommendation policies every contributor declares.
/// <para>
/// <b>Without this the platform boots with no strategies and no policies, which is not a degraded
/// state — it is a broken one.</b> Every surface would return an unranked pool with no exclusions:
/// blocked content, muted categories and members' own listings would all appear. There is no useful
/// behaviour behind an empty catalogue, which is why this runs at startup rather than on demand.
/// </para>
/// <para>
/// <b>A hosted service with a public, swallowing <see cref="SeedAsync"/>, and both halves of that
/// shape are a contract this platform has paid for.</b> The service runs at host start, before
/// migrations have necessarily been applied — so its first attempt may find no tables, and throwing
/// there takes the whole host down along with every other module's startup. The integration suite
/// creates its schema after the host starts and then calls <see cref="SeedAsync"/> again, which is
/// why the method is public and why every seeder must be idempotent by contract.
/// </para>
/// <para>
/// <b>Seeding creates and never overwrites.</b> A weight an operator has retuned is left alone: a
/// deployment that reverted every tuning decision on restart would make the whole
/// measure-and-adjust loop pointless, and the person who retuned it at three in the morning would
/// find it back where it started.
/// </para>
/// <para>
/// <b>A strategy whose key nothing implements is refused rather than seeded.</b> A row naming a
/// missing implementation contributes silent zeroes to every score on its surface — the worst kind
/// of defect, because everything still works and the answers are quietly worse. Logged as a warning
/// naming the key, which is where somebody can act on it.
/// </para>
/// </summary>
public sealed class RecommendationCatalogueSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<RecommendationCatalogueSeeder> _logger;

    public RecommendationCatalogueSeeder(
        IServiceScopeFactory scopes, ILogger<RecommendationCatalogueSeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>The module that owns this catalogue.</summary>
    public string Module => RecommendationsModuleMetadata.Name;

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

            await SeedStrategiesAsync(services, cancellationToken);
            await SeedPoliciesAsync(services, cancellationToken);

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
                "The recommendation catalogue could not be seeded yet. It will be retried when the "
                + "schema is available.");
        }
    }

    private async Task SeedStrategiesAsync(
        IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IRankingStrategyRepository>();
        var registry = services.GetRequiredService<IRankingStrategyRegistry>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        foreach (var contributor in services.GetServices<IRankingStrategyContributor>())
        {
            foreach (var seed in contributor.GetStrategies())
            {
                // A row naming a key nothing implements contributes silent zeroes. See the class
                // summary — refused rather than seeded.
                if (!registry.IsImplemented(seed.Key))
                {
                    RecommendationTelemetry.UnimplementedStrategies.Add(
                        1, new KeyValuePair<string, object?>("strategy", seed.Key));

                    _logger.LogWarning(
                        "Module '{Module}' contributed ranking strategy '{Key}' for the {Surface} "
                        + "surface, but nothing implements that key. It was not seeded.",
                        contributor.Module,
                        seed.Key,
                        seed.Surface);

                    continue;
                }

                if (await repository.ExistsAsync(seed.Key, seed.Surface, cancellationToken))
                {
                    // An operator may have retuned it. Never overwritten — see the class summary.
                    continue;
                }

                var strategy = RankingStrategy.Configure(
                    RankingStrategyId.From(guids.NewSequentialGuid()),
                    seed.Key,
                    seed.Kind,
                    seed.Surface,
                    seed.Description,
                    seed.Weight,
                    clock.UtcNow,
                    seed.TargetType,
                    seed.Order,
                    seed.RunsOnColdStart,
                    seed.MinimumScore);

                foreach (var (key, value) in seed.Parameters)
                {
                    strategy.SetParameter(key, value, guids.NewSequentialGuid());
                }

                repository.Add(strategy);
            }
        }
    }

    private async Task SeedPoliciesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IRecommendationPolicyRepository>();
        var rules = services.GetRequiredService<RecommendationPolicyRuleRegistry>();
        var guids = services.GetRequiredService<IGuidGenerator>();
        var clock = services.GetRequiredService<IClock>();

        foreach (var contributor in services.GetServices<IRecommendationPolicyContributor>())
        {
            var isPlatformContributor = string.Equals(
                contributor.Module, RecommendationsModuleMetadata.Name, StringComparison.Ordinal);

            foreach (var seed in contributor.GetPolicies())
            {
                if (!rules.IsImplemented(seed.Key))
                {
                    _logger.LogWarning(
                        "Module '{Module}' contributed recommendation policy '{Key}', but nothing "
                        + "implements that key. It was not seeded, and the rule it describes is not "
                        + "being enforced.",
                        contributor.Module,
                        seed.Key);

                    continue;
                }

                if (await repository.ExistsAsync(seed.Key, cancellationToken))
                {
                    continue;
                }

                // A vertical may contribute a rule; it may not contribute an undeletable one.
                // "Undeletable" is a platform guarantee rather than a vertical's preference.
                var isSystem = seed.IsSystem && isPlatformContributor;

                if (seed.IsSystem && !isPlatformContributor)
                {
                    _logger.LogWarning(
                        "Module '{Module}' contributed policy '{Key}' as a system policy. Only the "
                        + "Recommendation Platform may declare one; it was seeded as an ordinary "
                        + "policy.",
                        contributor.Module,
                        seed.Key);
                }

                var policy = RecommendationPolicy.Define(
                    RecommendationPolicyId.From(guids.NewSequentialGuid()),
                    seed.Key,
                    seed.Kind,
                    seed.Effect,
                    seed.Description,
                    clock.UtcNow,
                    seed.Order,
                    isSystem);

                policy.SetScope(seed.Surfaces, seed.TargetTypes, clock.UtcNow);

                if (seed.Effect == Domain.Enums.PolicyEffect.Demote)
                {
                    policy.SetDemotionFactor(seed.DemotionFactor, clock.UtcNow);
                }

                foreach (var (key, value) in seed.Parameters)
                {
                    policy.SetParameter(key, value, guids.NewSequentialGuid());
                }

                repository.Add(policy);
            }
        }
    }
}
