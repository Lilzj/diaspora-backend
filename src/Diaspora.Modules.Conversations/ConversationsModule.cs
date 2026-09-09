using System.Reflection;
using Diaspora.Modules.Conversations.Application.EventHandlers;
using Diaspora.Modules.Conversations.Application.Mapping;
using Diaspora.Modules.Conversations.Application.Services;
using Diaspora.Modules.Conversations.Domain.Blocks;
using Diaspora.Modules.Conversations.Domain.Catalog;
using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Extensibility;
using Diaspora.Modules.Conversations.Infrastructure;
using Diaspora.Modules.Conversations.Infrastructure.Hosting;
using Diaspora.Modules.Conversations.Options;
using Diaspora.Modules.Conversations.Persistence.Repositories;
using Diaspora.Modules.Conversations.Realtime;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using Diaspora.Platform.Operations.Health;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Modules.Conversations;

/// <summary>
/// B13.2's registration.
/// <para>
/// <b>Order 1500, after everything, and B13.3 changed what the number buys.</b> It used to be
/// load-bearing for correctness: this module replaced the Communication Runtime's shipped
/// <see cref="IRealtimeGroupAuthorizer"/> on last-registration-wins, so registering before B13
/// (order 1200) would have left the own-group default in place and every conversation group would
/// have stayed unjoinable with nothing reporting a fault. That is no longer how the authorizer
/// arrives — it claims the <c>conversation:</c> prefix on the kernel's channel seam, which composes
/// rather than replaces, and order cannot silence it.
/// </para>
/// <para>
/// The order still matters, for the ordinary reason every module's does: the seam registries are
/// built in module order, so a contributor from an earlier module is present when a later one scans.
/// It is no longer the only thing standing between the platform and a silent relay.
/// </para>
/// <para>
/// <b>The seams are scanned; everything else is registered by hand.</b> A context-type contributor,
/// a conversation policy or a message inspector may arrive from any assembly and is found by scan. A
/// repository, the mapper or the broadcaster is a collaborator of this module's own handlers, and
/// scanning for those would let an accidental second implementation elsewhere take over a write path
/// silently.
/// </para>
/// </summary>
public sealed class ConversationsModule : IPlatformModule
{
    public string Name => ConversationsModuleMetadata.Name;

    public string Version => ConversationsModuleMetadata.Version;

    /// <summary>See the class summary: this must be greater than the Notifications module's 1200.</summary>
    public int Order => 1500;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(ConversationsModule).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<ConversationOptions>()
            .Bind(configuration.GetSection(ConversationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterRealtime(services);
        RegisterIntegrations(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IConversationMessageRepository, ConversationMessageRepository>();
        services.AddScoped<IConversationBlockRepository, ConversationBlockRepository>();
        services.AddScoped<IConversationContextTypeRepository, ConversationContextTypeRepository>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // The one door to a conversation. Every handler goes through it so that "not yours" and
        // "does not exist" cannot be told apart at any call site.
        services.AddScoped<ConversationAccessService>();

        services.AddScoped<ConversationContextCatalog>();
        services.AddScoped<ConversationMapper>();

        // Applies mute and block, then publishes. It does not create notifications — see DSP2003
        // and the class summary.
        services.AddScoped<ConversationNotifier>();

        // The single translation point where a domain fact becomes a published contract.
        services.AddScoped<ConversationIntegrationEventRelay>();
    }

    /// <summary>
    /// The extension points, scanned across every platform assembly, each with a shipped default.
    /// <para>
    /// The defaults are registered first and replaced by last-registration-wins, which is the same
    /// substitution B13 uses. A deployment that wants member names registers its own directory after
    /// this module; nothing else changes.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        // Reference data and additive rules: any number of implementations, all consulted.
        RegisterAll<IConversationContextTypeContributor>(services, assemblies);
        RegisterAll<IConversationPolicy>(services, assemblies);
        RegisterAll<IConversationMessageInspector>(services, assemblies);

        // Single-implementation seams. Each ships a default that is a complete answer rather than a
        // stub — see ConversationDefaults.
        // **The real one, not the placeholder.** `IdentifierOnlyParticipantDirectory` is what
        // shipped, and nothing ever replaced it — so every conversation on the platform came back
        // with a counterparty carrying no name and the client rendered every thread as "Unknown".
        // `MemberParticipantDirectory` reads the kernel's member port, which the identity layer
        // answers, and falls back to the same silence the placeholder gave when that port is
        // itself a placeholder.
        services.AddScoped<IConversationParticipantDirectory, MemberParticipantDirectory>();
        services.AddScoped<IConversationContextResolver, UnavailableContextResolver>();
        services.AddScoped<IConversationAttachmentValidator, DefaultAttachmentValidator>();
        services.AddScoped<IConversationSearchEngine, EfConversationSearchEngine>();
    }

    /// <summary>
    /// The realtime wiring, and the reason this module exists at order 1500.
    /// <para>
    /// <b>No <c>AddSignalR</c>, no hub, no transport.</b> This module names SignalR nowhere and an
    /// architecture test asserts it. What it registers is an answer to a question B13's runtime
    /// could not answer on its own — who is in a conversation — and a broadcaster that writes
    /// through B13's own <see cref="IRealtimeNotifier"/>.
    /// </para>
    /// </summary>
    private static void RegisterRealtime(IServiceCollection services)
    {
        // ConversationRealtimeGroupAuthorizer is NOT registered here, and its absence is the point.
        //
        // B13.3 moved it off IRealtimeGroupAuthorizer — which it used to replace outright — onto the
        // kernel's prefix seam, where the Communication Platform scans for it across every platform
        // assembly exactly as it scans for channels and template contributors. Registering it here
        // as well would put two instances in the container claiming one prefix, and the channel
        // registry refuses that at startup. Nothing about the answer changed; what changed is that a
        // second vertical claiming a different prefix now sits beside it instead of silently
        // switching it off, and module order no longer decides whether conversations relay at all.
        services.AddScoped<ConversationBroadcaster>();

        services.AddScoped<IModuleHealthContributor, ConversationHealthContributor>();
    }

    /// <summary>
    /// Ports other modules published and left empty.
    /// <para>
    /// B10 shipped <c>IMarketplaceConversationProvider</c> with no implementation and recorded
    /// buyer/seller chat as "future Communication". Registering the adapter here is what that note
    /// meant — and it is the only reason this module references Marketplace at all.
    /// </para>
    /// </summary>
    private static void RegisterIntegrations(IServiceCollection services) =>
        services.AddScoped<
            Diaspora.Modules.Marketplace.Extensibility.Conversations.IMarketplaceConversationProvider,
            Integration.Marketplace.MarketplaceConversationAdapter>();

    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<ConversationCatalogueSeeder>();

        // B23.x Wave 12. Publishes the two facts a conversation produces by *not* changing —
        // `ConversationInactivityDetected` and `ConversationUnreadThresholdReached` — because the
        // Phase 18 automation catalogue offers a trigger for each and deriving either outside this
        // module would mean another module reading message state.
        services.AddHostedService<Infrastructure.Hosting.ConversationAttentionWorker>();
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
