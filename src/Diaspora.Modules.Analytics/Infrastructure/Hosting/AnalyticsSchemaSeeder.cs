using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Identifiers;
using Diaspora.Modules.Analytics.Domain.Policies;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Domain.Schema;
using Diaspora.Platform.Modules.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Analytics.Infrastructure.Hosting;

/// <summary>
/// Puts the event schema registry into the database at startup.
/// <para>
/// <b>This is the registry FRONTEND-PHASE15-GAP-029 asks for by name</b>, and seeding it is what
/// makes an unregistered event a visible refusal rather than a silent drop. A deployment whose
/// registry is empty refuses every submission with a named code — which is loud, and is the correct
/// failure. The alternative, an ingestion endpoint that accepts anything it is sent, would make the
/// client's four-phase-old property sanitiser advisory.
/// </para>
/// <para>
/// <b>Idempotent by (name, version), and existing rows are left alone.</b> A schema change is a new
/// row, never an edit, so a row already in the raw store keeps naming a contract that exists and a
/// three-month-old chart can still say which definition produced it.
/// </para>
/// <para>
/// <b>A <see cref="BackgroundService"/> with a public <see cref="SeedAsync"/>, and every failure
/// swallowed.</b> The platform's registration contract, restated once too often to restate again:
/// as a hosted service a fresh deployment works, as a public method the integration factory awaits
/// it deterministically, and an exception out of a hosted service takes the whole host down at
/// startup rather than leaving one module short of a catalogue.
/// </para>
/// </summary>
public sealed class AnalyticsSchemaSeeder : BackgroundService, IReferenceDataSeeder
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AnalyticsSchemaSeeder> _logger;

    public AnalyticsSchemaSeeder(IServiceScopeFactory scopes, ILogger<AnalyticsSchemaSeeder> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    public string Module => AnalyticsModuleMetadata.Name;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) => SeedAsync(stoppingToken);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopes.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<IAnalyticsEventSchemaRepository>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var existing = (await repository.ListAsync(cancellationToken).ConfigureAwait(false))
                .Select(schema => (schema.EventType, schema.Version))
                .ToHashSet();

            var added = 0;

            foreach (var seed in Catalogue())
            {
                if (existing.Contains((seed.EventType, seed.Version)))
                {
                    continue;
                }

                repository.Add(AnalyticsEventSchema.Register(
                    AnalyticsEventSchemaId.New(),
                    seed.EventType,
                    seed.Version,
                    seed.TrustClass,
                    seed.AllowsClientSubmission,
                    seed.DedupStrategy,
                    seed.RequiresContent,
                    seed.Description));

                added++;
            }

            if (added > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown during startup. Nothing to report.
        }
#pragma warning disable CA1031 // A seeding failure must not take the host down. See the summary.
        catch (Exception exception)
        {
            _logger.LogError(exception, "Seeding the analytics event schema registry failed.");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// The registry, version 1.
    /// <para>
    /// <b>Three of the eleven accept a client submission and eight do not.</b> The eight are facts a
    /// module already commits inside its own transaction and publishes; a client posting one of them
    /// would be incrementing a business metric, which §41 forbids outright. The three the client may
    /// send are the ones no server contract carries — somebody looked at something — and each is
    /// deduplicated so that looking twice is not two facts.
    /// </para>
    /// </summary>
    private static IEnumerable<SchemaSeed> Catalogue()
    {
        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentViewed,
            1,
            AnalyticsTrustClass.Client,
            AllowsClientSubmission: true,
            AnalyticsDedupStrategy.PerActorPerContentPerDay,
            RequiresContent: true,
            "Somebody opened a piece of content. One person counted once per item per day, which is "
                + "the published definition of a view rather than a storage convenience.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ProfileViewed,
            1,
            AnalyticsTrustClass.Client,
            AllowsClientSubmission: true,
            AnalyticsDedupStrategy.PerActorPerSubjectPerDay,
            RequiresContent: true,
            "Somebody opened a creator's public profile. One person counted once a day.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentImpression,
            1,
            AnalyticsTrustClass.Client,
            AllowsClientSubmission: true,
            AnalyticsDedupStrategy.PerActorPerContentPerDay,
            RequiresContent: true,
            "Somebody saw content in a feed or a search result without opening it. Accepted and "
                + "stored; no metric is served from it yet, because reach is not published.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentPublished,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: true,
            "Content became readable by somebody other than its owner. First publication only.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentSaved,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: true,
            "Somebody saved a piece of content to come back to.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentShared,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: true,
            "Somebody shared a piece of content. The share kind travels; the recipient does not.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentCommented,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: true,
            "Somebody other than the owner commented on a piece of content.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentReacted,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: true,
            "Somebody other than the owner reacted to a piece of content. The reaction key travels, "
                + "so one stored fact serves both engagement and helpful votes.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.ContentConverted,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: true,
            "Somebody took the action the content asked for: an RSVP, a job application, a purchase "
                + "or a viewing.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AudienceFollowed,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "Somebody started following a member.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AudienceUnfollowed,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "Somebody stopped following a member.");

        foreach (var seed in AutomationCatalogue())
        {
            yield return seed;
        }
    }

    /// <summary>
    /// The automation vocabulary, version 1 — B24 · FRONTEND-PHASE18-GAP-028.
    /// <para>
    /// <b>Twelve names, all of them the client's own.</b> The Phase 18 register recorded this as "the
    /// sixth restatement of one missing endpoint"; the endpoint shipped with B19 and the vocabulary
    /// did not, so every automation event the client has flushed since has been refused. Wave 11's
    /// report marked the gap closed on the strength of the endpoint existing. It was not.
    /// </para>
    /// <para>
    /// <b>None of the twelve requires content, and that is the shape of the difference.</b> Every
    /// event above is about a piece of content somebody made; these are about a rule somebody wrote,
    /// and a rule is not content — it is not published, not readable by anybody else, and not
    /// attributable to a creator. They are stored with no content reference and no owner
    /// attribution, which keeps them out of the creator rollups entirely: an automation run appearing
    /// in somebody's content analytics would be a category error with a number attached.
    /// </para>
    /// <para>
    /// <b>The four client-submittable ones deduplicate on their own emit time.</b> The client sets
    /// <c>occurredAt</c> when the event happens rather than when it is delivered, so a retried flush
    /// of the same buffered record composes the same key and is recognised as a duplicate. That is
    /// §30 satisfied without the batch identifier the register asks for — the identity is the fact,
    /// not the delivery.
    /// </para>
    /// </summary>
    private static IEnumerable<SchemaSeed> AutomationCatalogue()
    {
        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationCreated,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "Somebody created an automation. The trigger key travels; the rule's contents never do.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationActivated,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "An automation was switched on. The denominator every execution figure is read against.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationPaused,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "An automation was paused. Distinct from disabled, because members mean different "
                + "things by the two and use both.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationDisabled,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "An automation was switched off.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationDeleted,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "An automation was removed.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationExecuted,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "An automation ran and did not fail. The terminal status travels as the qualifier, so "
                + "a skip — the commonest healthy outcome — is separable from a completion.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationFailed,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "An automation ran and failed. The failure kind travels as the qualifier; the failure "
                + "message never does.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationTemplateUsed,
            1,
            AnalyticsTrustClass.Server,
            AllowsClientSubmission: false,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "An automation was built from a catalogue template. The template id is platform "
                + "vocabulary, not anything a member wrote.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationTested,
            1,
            AnalyticsTrustClass.Client,
            AllowsClientSubmission: true,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "Somebody ran a rule as a dry run. Client-attested because a simulation performs "
                + "nothing and is never persisted, so no server fact exists to derive it from.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationBuilderStarted,
            1,
            AnalyticsTrustClass.Client,
            AllowsClientSubmission: true,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "Somebody opened the automation builder.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationBuilderAbandoned,
            1,
            AnalyticsTrustClass.Client,
            AllowsClientSubmission: true,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "Somebody opened the builder and left without saving. The one number a builder is "
                + "judged by, and the one no server record can produce.");

        yield return new SchemaSeed(
            AnalyticsEventTypes.AutomationGapEncountered,
            1,
            AnalyticsTrustClass.Client,
            AllowsClientSubmission: true,
            AnalyticsDedupStrategy.PerSourceEvent,
            RequiresContent: false,
            "A member met a capability this platform does not have. Turns the gap register into a "
                + "ranked list of what members are actually blocked by.");
    }

    private sealed record SchemaSeed(
        string EventType,
        int Version,
        AnalyticsTrustClass TrustClass,
        bool AllowsClientSubmission,
        AnalyticsDedupStrategy DedupStrategy,
        bool RequiresContent,
        string Description);
}
