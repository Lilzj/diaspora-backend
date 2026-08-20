using System.Reflection;
using Diaspora.Platform.Messaging.Outbox;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Administration;
using Diaspora.Platform.Messaging.Outbox.Dispatch;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Partitioning;
using Diaspora.Platform.Messaging.Outbox.Retry;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using Diaspora.Platform.Messaging.Outbox.Telemetry;
using Diaspora.Platform.Messaging.Outbox.Tenancy;
using Diaspora.Platform.Modules;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Messaging;

/// <summary>
/// Registers the transport-independent half of reliable messaging.
/// <para>
/// <b>Nothing here knows about a database or a mediator.</b> Serialization, partitioning,
/// retry policy, statistics and the operator surface are all decisions the platform makes
/// regardless of where messages are stored or how they are eventually published — the storage
/// adapter and the transport are registered separately, by the layers that own those choices.
/// That separation is what makes swapping either one a single registration change.
/// </para>
/// <para>
/// <b>No business module calls this, or anything it registers.</b> The outbox is composed
/// entirely at the platform level; a module publishes an integration event exactly as it did
/// before B1.1 and never learns that delivery became durable.
/// </para>
/// </summary>
public static class PlatformMessagingRegistration
{
    public static IServiceCollection AddPlatformMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<Assembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        AddOptions(services, configuration);

        // Built once from the scanned assemblies and shared. Discovery is reflective and the
        // catalogue never changes after startup; rebuilding it per scope would put an assembly
        // scan on the path of every command that publishes an event.
        var scanned = assemblies?.ToArray() ?? ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        services.AddSingleton<IIntegrationEventTypeResolver>(
            _ => new IntegrationEventTypeResolver(scanned));

        services.AddSingleton<IOutboxSerializer, JsonOutboxSerializer>();

        // Contributed as a set, so a module that needs different grouping adds its own strategy
        // ahead of the convention instead of replacing it for everybody.
        services.AddSingleton<IOutboxPartitionKeyStrategy, ConventionOutboxPartitionKeyStrategy>();
        services.AddSingleton<OutboxPartitionKeyResolver>();

        // TryAdd, so a future tenancy module replaces it without touching this file.
        services.TryAddSingleton<IOutboxTenantResolver, SingleTenantOutboxTenantResolver>();

        services.TryAddSingleton<IOutboxRetryPolicy, ExponentialBackoffRetryPolicy>();

        services.AddSingleton<OutboxStatisticsSnapshot>();

        // Held as a singleton because an observable instrument with no live reference stops
        // reporting after a collection — silently, and some time later, which is the worst
        // possible way for a dashboard to break.
        services.AddSingleton<OutboxGaugeRegistration>();

        services.AddScoped<OutboxMessageFactory>();
        services.AddScoped<IOutboxDispatcher, Outbox.Dispatch.OutboxDispatcher>();
        services.AddScoped<IOutboxAdministration, OutboxAdministrationService>();

        return services;
    }

    private static void AddOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // The cross-field rules: a publish timeout longer than its own lease, a dead-letter
        // retention shorter than the delivered one, concurrency that would break the ordering
        // guarantee the same configuration asks for. Every one of them passes range validation
        // and then misbehaves in production.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<OutboxOptions>, OutboxOptionsValidator>());
    }
}
