using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Extensibility.Actions;
using Diaspora.Modules.Automation.Extensibility.Scopes;
using Diaspora.Modules.Automation.Infrastructure.Hosting;
using Diaspora.Modules.Automation.Observability;
using Diaspora.Modules.Automation.Options;
using Diaspora.Modules.Automation.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Operations.Alerting;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diaspora.Modules.Automation;

/// <summary>
/// The Automation Platform Foundation.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so it
/// lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, tables, telemetry and the B13 notification catalogue are all found by the kernel's
/// contributor scan. <c>Program.cs</c>, the Platform Kernel, Identity and Notifications are untouched
/// except for the one consumer GAP-024 requires, which is documented where it lives.
/// </para>
/// <para>
/// <b>Order 2100, after everything.</b> The number is a dependency direction on this platform — a
/// module may reference one composed before it and never one composed after — and this module reads
/// one: Notifications at 1200, for its catalogue contributor seams. Composing last also means nothing
/// else can depend on it, which is the correct shape for an orchestration layer: a business module
/// that took a reference on the automation platform would be a business module whose domain logic
/// could be reached by a member's rule.
/// </para>
/// <para>
/// <b>Every extension seam is <c>TryAdd</c>, so a module's real answer replaces the shipped floor
/// rather than stacking behind it.</b> The scope authority is the one that matters: a module that
/// knows who moderates a community registers <c>IAutomationScopeAuthority</c> before this composes
/// and the resolver prefers it, because it names the owner kinds it handles and the floor does not.
/// </para>
/// </summary>
public sealed class AutomationModule : IPlatformModule
{
    public string Name => AutomationModuleMetadata.Name;

    public string Version => AutomationModuleMetadata.Version;

    public int Order => 2100;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(AutomationModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<AutomationOptions>()
            .Bind(configuration.GetSection(AutomationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterEngine(services);
        RegisterSeams(services);
        RegisterHosting(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IAutomationRepository, AutomationRepository>();
        services.AddScoped<IAutomationRunRepository, AutomationRunRepository>();
        services.AddScoped<IAutomationOccurrenceRepository, AutomationOccurrenceRepository>();
        services.AddScoped<IAutomationSubjectHorizonRepository, AutomationSubjectHorizonRepository>();
        services.AddScoped<IAutomationReminderRepository, AutomationReminderRepository>();
        services.AddScoped<ISavedSearchRepository, SavedSearchRepository>();

        // The clock, defensively. The host registers one; a module that assumed so and was composed
        // into a bare test container would fail on resolution rather than on a wrong date.
        services.TryAddSingleton(TimeProvider.System);
    }

    private static void RegisterEngine(IServiceCollection services)
    {
        // The evaluator and the calculator are static classes rather than registrations: they hold no
        // state and take no dependency, and CA1822 reports every stateless instance method as one in
        // Release. A registration for a pure function is a lifetime attached to nothing.

        services.AddScoped<AutomationActionDispatcher>();
        services.AddScoped<AutomationEngine>();
        services.AddScoped<AutomationRuleValidator>();
        services.AddScoped<AutomationAccessService>();
        services.AddScoped<AutomationScheduleService>();
        services.AddScoped<AutomationSubjectHorizonService>();
        services.AddScoped<AutomationConditionResolver>();
        services.AddScoped<SavedSearchMatcher>();

        // B24. The reporter is scoped because it publishes into the caller's unit of work; the
        // classifier is scoped because it holds B17's registered failure classifier, whose lifetime
        // is the deployment's to choose. Neither is a background service and neither saves.
        services.AddScoped<AutomationRunReporter>();
        services.AddScoped<AutomationFailureClassifier>();
        services.AddScoped<AutomationDiagnosticsService>();

        // B24-G65 · §39. Registered here rather than found by the kernel scan, because
        // IAlertRuleContributor is not one of the interfaces the kernel scans for — B17 composes the
        // catalogue from whatever is in the container. A module that wants rules registers them, the
        // catalogue picks them up, and the host still names nothing. Widening the kernel's scan would
        // have been a platform change made to save one line here.
        services.AddSingleton<IAlertRuleContributor, AutomationAlertRuleContributor>();
    }

    /// <summary>
    /// Registers the extension boundary and its shipped defaults.
    /// <para>
    /// <b>The defaults are registered rather than omitted.</b> A missing registration would produce a
    /// resolution failure at the first request; a registered default that answers honestly produces an
    /// answer a client can branch on and gives the capability report something true to say. PSR-1.2's
    /// rule — "a stub is worse than an absence" — is satisfied because neither of these pretends the
    /// capability exists: the scope authority grants nothing beyond a member's own affairs, and the
    /// calendar writer reports that this deployment cannot write a calendar entry.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAutomationScopeAuthority, SelfOnlyScopeAuthority>());

        services.AddScoped<AutomationScopeAuthorityResolver>();

        services.TryAddScoped<IAutomationCalendarWriter, NotConfiguredCalendarWriter>();
    }

    /// <summary>
    /// Registers the sweep.
    /// <para>
    /// One <c>BackgroundService</c>, which takes B17's distributed lease before touching anything. It
    /// claims due occurrences, delivers due reminders, times out runs whose settlement never arrived,
    /// re-derives the next occurrence for every active schedule, and applies the retention horizon.
    /// </para>
    /// <para>
    /// <b>It is not a second scheduler.</b> B17's shipped <c>InMemoryJobScheduler</c> documents itself
    /// as non-durable — "jobs do not survive a restart… anything that must happen goes through the
    /// outbox" — and a member's 07:45 reminder must happen. What this owns is a durable due-store with
    /// a claim protocol, which is precisely the half B17's implementation says it lacks. It registers
    /// no job type, no cron expression and no handler dispatch.
    /// </para>
    /// </summary>
    private static void RegisterHosting(IServiceCollection services) =>
        services.AddHostedService<AutomationSchedulerWorker>();
}
