using Diaspora.Modules.Notifications.Application.Delivery;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Application.EventHandlers;
using Diaspora.Modules.Notifications.Application.Preferences;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Application.Templates;
using Diaspora.Modules.Notifications.Domain.Analytics;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Devices;
using Diaspora.Modules.Notifications.Domain.Digests;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Domain.Templates;
using Diaspora.Modules.Notifications.Extensibility.Addressing;
using Diaspora.Modules.Notifications.Extensibility.Audiences;
using Diaspora.Modules.Notifications.Extensibility.Caching;
using Diaspora.Modules.Notifications.Extensibility.Catalog;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Extensibility.DeepLinks;
using Diaspora.Modules.Notifications.Extensibility.Devices;
using Diaspora.Modules.Notifications.Extensibility.Digests;
using Diaspora.Modules.Notifications.Extensibility.Routing;
using Diaspora.Modules.Notifications.Extensibility.Search;
using Diaspora.Modules.Notifications.Extensibility.Templates;
using Diaspora.Modules.Notifications.Extensibility.Throttling;
using Diaspora.Modules.Notifications.Infrastructure.Addressing;
using Diaspora.Modules.Notifications.Infrastructure.Audiences;
using Diaspora.Modules.Notifications.Infrastructure.Caching;
using Diaspora.Modules.Notifications.Infrastructure.Channels;
using Diaspora.Modules.Notifications.Infrastructure.DeepLinks;
using Diaspora.Modules.Notifications.Infrastructure.Devices;
using Diaspora.Modules.Notifications.Infrastructure.Digests;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Hosting;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Templates;
using Diaspora.Modules.Notifications.Infrastructure.Throttling;
using Diaspora.Modules.Notifications.Options;
using Diaspora.Modules.Notifications.Persistence.Repositories;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.Platform.Communication;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Operations.Health;
using Diaspora.SharedKernel.Primitives;
using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Notifications;

/// <summary>
/// B13's registration.
/// <para>
/// <b>Order 1200, last.</b> Communication consumes what every other module publishes and is consumed
/// by none of them, so it belongs at the end of the graph — and the seam registries are built in
/// module order, which means a channel or a template contributed by an earlier module is already
/// present when this one scans for them.
/// </para>
/// <para>
/// <b>The seams are scanned; everything else is registered by hand.</b> A channel, a template
/// contributor, a category contributor or a rule contributor is a seam and may arrive from any
/// assembly. A repository, a resolver or the relay is a collaborator of this module's own handlers,
/// and scanning for those would let an accidental second implementation elsewhere silently take over
/// a write path.
/// </para>
/// </summary>
public sealed class NotificationsModule : IPlatformModule
{
    public string Name => NotificationsModuleMetadata.Name;

    public string Version => NotificationsModuleMetadata.Version;

    public int Order => 1200;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(NotificationsModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviours
        // are open generics registered once, so validation, telemetry, transactions, authorisation
        // and audit apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RealtimeOptions>()
            .Bind(configuration.GetSection(RealtimeOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PushProviderOptions>()
            .Bind(configuration.GetSection(PushProviderOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterRealtimeRuntime(services, configuration);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<Realtime.AccessStateRealtimeRelay>();
        services.AddScoped<IUserInboxRepository, UserInboxRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<INotificationCategoryRepository, NotificationCategoryRepository>();
        services.AddScoped<INotificationRuleRepository, NotificationRuleRepository>();
        services.AddScoped<IDeviceRegistrationRepository, DeviceRegistrationRepository>();
        services.AddScoped<INotificationDigestRepository, NotificationDigestRepository>();
        services.AddScoped<INotificationAnalyticsRepository, NotificationAnalyticsRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<NotificationAuthorizationService>();
        services.AddScoped<NotificationMutationService>();
        services.AddScoped<NotificationTemplateCatalog>();
        services.AddScoped<NotificationAnalyticsRecorder>();
        services.AddScoped<InboxCounterService>();
        services.AddScoped<TemplateAssembler>();

        // B13.3. Signs and verifies the token in an email footer. Scoped rather than singleton
        // because it reads the clock through the platform's port, and a singleton holding a scoped
        // dependency is the lifetime bug that only shows up under load.
        services.AddScoped<UnsubscribeTokenFactory>();
        services.AddScoped<NotificationTemplateSeedAssembler>();

        // The dispatcher and everything it needs to decide. Registered explicitly rather than
        // scanned because it is the one door through which a notification is created, and a second
        // implementation appearing by accident would be the defect hardest to notice.
        services.AddScoped<DeliveryPolicyResolver>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<NotificationDeliveryRunner>();

        // The single translation point where a domain fact becomes a published contract.
        services.AddScoped<NotificationIntegrationEventRelay>();
    }

    /// <summary>
    /// The extension points, scanned across every platform assembly.
    /// <para>
    /// A default is registered for each single-implementation seam <em>before</em> nothing else can
    /// claim it, using <c>TryAdd</c> semantics in spirit: the shipped renderer, deep-link resolver,
    /// digest generator, device registry, address resolver, audience resolver, throttle and search
    /// engine are all replaceable, and a deployment replaces one by registering its own after this
    /// module — which is what <c>AddScoped</c>'s last-wins behaviour gives.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        // Reference data. Any module may contribute its own categories, rules and wording without
        // this module referencing it.
        RegisterAll<INotificationCategoryContributor>(services, assemblies);
        RegisterAll<INotificationRuleContributor>(services, assemblies);
        RegisterAll<INotificationTemplateContributor>(services, assemblies);

        // Channels. Exactly one implementation per kind; the registry fails startup on a duplicate.
        RegisterAll<INotificationChannel>(services, assemblies);
        services.AddScoped<NotificationChannelRegistry>();

        // B13.3. Real-time channel families, contributed by whoever owns the subject. Scanned for
        // the same reason channels and templates are — a vertical must be able to say who may listen
        // to its own records without this module referencing it, and, since Community Life registers
        // before this one, without being able to reference this module either. Exactly one
        // contributor per prefix; the registry fails startup on a duplicate.
        RegisterAll<IRealtimeChannelAuthorizer>(services, assemblies);
        services.AddScoped<RealtimeChannelRegistry>();

        // The transports behind the channels. Neither has a vendor in it; both exercise the whole
        // path above them and report that nothing is configured. The push transport is chosen by
        // configuration in RegisterRealtimeRuntime, because a deployment may ask for the loopback
        // sender instead — and a real adapter replaces whichever is registered here by registering
        // after this module.
        services.AddScoped<IEmailSender, UnconfiguredEmailSender>();

        services.AddScoped<ITemplateRenderer, DefaultTemplateRenderer>();
        services.AddScoped<IDigestGenerator, DefaultDigestGenerator>();
        services.AddScoped<IDeviceRegistry, DeviceRegistrationRegistry>();
        services.AddScoped<IRecipientAddressResolver, NullRecipientAddressResolver>();
        services.AddScoped<INotificationAudienceResolver, EmptyNotificationAudienceResolver>();
        services.AddScoped<INotificationThrottle, SubjectRateLimitNotificationThrottle>();
        services.AddScoped<INotificationSearchEngine, EfNotificationSearchEngine>();

        // Two singletons, and safe as such: one holds a route list and no state, the other a
        // concurrent dictionary and no scoped dependency. The cache is also the one component whose
        // whole value is that it outlives a request.
        services.AddSingleton<IDeepLinkResolver, DefaultDeepLinkResolver>();
        services.AddSingleton<INotificationTemplateCache, InMemoryNotificationTemplateCache>();
    }

    /// <summary>
    /// The real-time runtime and the push transport, both registered from here and from nowhere else.
    /// <para>
    /// <b><c>AddSignalR</c> is called by a module, which is the point.</b> Nothing in
    /// <c>Program.cs</c> knows real-time exists; the hub is mapped by this module's endpoint
    /// contributor and the token that authenticates it is accepted by the host's generic
    /// <c>/hubs</c> rule. A vertical that one day owns conversations adds its own hub the same way.
    /// </para>
    /// <para>
    /// <b>The state-holding components are singletons and must be.</b> A connection registry with a
    /// request lifetime would forget every socket the moment the request that created it ended,
    /// which is a platform where nobody is ever online and nothing reports a fault. The two that
    /// consult per-request state — the group authorizer and, through it, the coordinator — are
    /// scoped, because the implementation that eventually answers "is this member in this
    /// conversation" will read a table.
    /// </para>
    /// </summary>
    private static void RegisterRealtimeRuntime(
        IServiceCollection services, IConfiguration configuration)
    {
        var realtime = configuration.GetSection(RealtimeOptions.SectionName).Get<RealtimeOptions>()
            ?? new RealtimeOptions();

        var push = configuration.GetSection(PushProviderOptions.SectionName).Get<PushProviderOptions>()
            ?? new PushProviderOptions();

        // Chosen here rather than by a factory that inspects options per resolution, so the startup
        // validator can read the registration and say what is actually wired. A real adapter
        // registers after this module and wins on last-registration-wins, which is the same
        // substitution every other seam in this module uses.
        if (push.IsLoopback)
        {
            services.AddScoped<IPushNotificationSender, LoopbackPushNotificationSender>();
        }
        else
        {
            services.AddScoped<IPushNotificationSender, UnconfiguredPushNotificationSender>();
        }

        // Registered whether or not real-time is enabled: the health contributor reports on it, the
        // startup validator reads its options, and the in-app channel resolves the broadcaster on
        // every delivery. With the runtime off they all resolve and none of them does work.
        services.AddSingleton<IRealtimeConnectionRegistry, InMemoryRealtimeConnectionRegistry>();
        services.AddSingleton<IPresenceStore, InMemoryPresenceStore>();
        services.AddSingleton<IRealtimeBackplane, SingleNodeRealtimeBackplane>();

        // The port is unchanged; what stands behind it is not. See CompositeRealtimeGroupAuthorizer
        // for why the own-group default could not stay: it was replaceable by exactly one module,
        // and the second to try would have silently switched off the first.
        services.AddScoped<IRealtimeGroupAuthorizer, CompositeRealtimeGroupAuthorizer>();
        services.AddScoped<RealtimeConnectionCoordinator>();
        services.AddScoped<RealtimeInboxBroadcaster>();
        services.AddScoped<RealtimeChannelBroadcaster>();

        services.AddScoped<IModuleHealthContributor, RealtimeHealthContributor>();

        if (!realtime.Enabled)
        {
            // The notifier still has to resolve, and it must not be the SignalR one: AddSignalR is
            // never called below, so IHubContext<RealtimeHub> is not in the container and every
            // in-app delivery would throw on a deployment whose only sin was setting one flag to
            // false. Nothing at startup would catch that — the failure arrives in the delivery
            // sweep, in production.
            services.AddSingleton<IRealtimeNotifier, DisabledRealtimeNotifier>();

            // No hub, no transport, no protocol. The endpoint contributor makes the same decision
            // from the same option, so a disabled deployment has no route either.
            return;
        }

        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

        // Replaces SignalR's default, which reads the wrong claim for this platform. Registered
        // before AddSignalR so it is in the container when the hub lifetime manager resolves it; see
        // RealtimeUserIdProvider for what the default gets wrong and why nothing would report it.
        services.AddSingleton<IUserIdProvider, RealtimeUserIdProvider>();

        services
            .AddSignalR(options =>
            {
                options.KeepAliveInterval = TimeSpan.FromSeconds(realtime.KeepAliveSeconds);
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(realtime.ClientTimeoutSeconds);
                options.HandshakeTimeout = TimeSpan.FromSeconds(realtime.HandshakeTimeoutSeconds);
                options.MaximumReceiveMessageSize = realtime.MaxReceiveMessageBytes;
                options.EnableDetailedErrors = realtime.EnableDetailedErrors;
            })
            .AddJsonProtocol(options =>
            {
                // The same two decisions the HTTP surface makes in Program.cs. A socket that
                // camel-cased differently from the REST API would give one client two shapes for one
                // notification, and a strongly typed identifier serialised as an object rather than
                // a bare Guid is the defect that would be found by a mobile developer rather than by
                // this repository.
                options.PayloadSerializerOptions.PropertyNamingPolicy =
                    System.Text.Json.JsonNamingPolicy.CamelCase;

                options.PayloadSerializerOptions.Converters.Add(
                    new StronglyTypedIdJsonConverterFactory());
            });
    }

    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<NotificationCatalogueSeeder>();
        services.AddHostedService<NotificationDeliverySweep>();
        services.AddHostedService<NotificationDigestSweep>();
        services.AddHostedService<NotificationRetentionSweep>();
        services.AddHostedService<InboxReconciliationSweep>();
    }

    private static void RegisterAll<TContract>(
        IServiceCollection services, IReadOnlyList<Assembly> assemblies)
    {
        foreach (var type in ModuleAssemblyScanner.FindImplementations<TContract>(assemblies))
        {
            services.AddScoped(typeof(TContract), type);
        }
    }
}
