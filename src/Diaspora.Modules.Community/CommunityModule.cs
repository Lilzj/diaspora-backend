using Diaspora.Modules.Community.Application.EventHandlers;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Bookmarks;
using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Feeds;
using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Modules.Community.Domain.Hashtags;
using Diaspora.Modules.Community.Domain.Members;
using Diaspora.Modules.Community.Domain.Mentions;
using Diaspora.Modules.Community.Domain.Moderation;
using Diaspora.Modules.Community.Domain.Polls;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Modules.Community.Domain.Shares;
using Diaspora.Modules.Community.Domain.Visibility;
using Diaspora.Modules.Community.Extensibility.Content;
using Diaspora.Modules.Community.Extensibility.Enrichment;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Hashtags;
using Diaspora.Modules.Community.Extensibility.Mentions;
using Diaspora.Modules.Community.Extensibility.Moderation;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.Modules.Community.Extensibility.Reactions;
using Diaspora.Modules.Community.Extensibility.Visibility;
using Diaspora.Modules.Community.Infrastructure.Hosting;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.Community.Options;
using Diaspora.Modules.Community.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Modules.Seeding;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Diaspora.Modules.Community;

/// <summary>
/// The Community &amp; Social Foundation Engine.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so
/// it lands in the build output and names it nowhere else; permissions, feature gates, policies,
/// endpoints, telemetry and database configuration are all found by the kernel's contributor
/// scan. <c>Program.cs</c>, the Platform Kernel, Identity, Trust, Location and Media are
/// untouched.
/// </para>
/// <para>
/// <b>Order 500.</b> After Identity (1), Bookmarks (100), Trust (200), Location (300) and Media
/// (400). Registration order is not load-bearing — every cross-module dependency here is
/// resolved lazily through the container — but ordering last matches the dependency direction:
/// Community consumes all five and is consumed by every content module after it.
/// </para>
/// <para>
/// <b>Eight extension seams are assembly-scanned</b>, which is the whole architectural claim of
/// this module: a feed, a ranking, a reaction kind, a content source, a mention resolver, a
/// hashtag processor, a visibility evaluator, a review provider, a moderation policy and a
/// content enricher are all classes in whichever assembly owns the idea. This file gains no line
/// when one is added.
/// </para>
/// </summary>
public sealed class CommunityModule : IPlatformModule
{
    public string Name => CommunityModuleMetadata.Name;

    public string Version => CommunityModuleMetadata.Version;

    public int Order => 500;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(CommunityModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline behaviors
        // are open generics registered once, so validation, telemetry, transactions and
        // authorisation apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddOptions<CommunityOptions>()
            .Bind(configuration.GetSection(CommunityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        RegisterRepositories(services);
        RegisterServices(services);
        RegisterSeams(services);
        RegisterConsumerContracts(services);
        RegisterHostedServices(services);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<ICommunityPostRepository, CommunityPostRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IReactionRepository, ReactionRepository>();
        services.AddScoped<IReactionDefinitionRepository, ReactionDefinitionRepository>();
        services.AddScoped<IShareRepository, ShareRepository>();
        services.AddScoped<IBookmarkRepository, BookmarkRepository>();
        services.AddScoped<IFollowRepository, FollowRepository>();
        services.AddScoped<IHashtagRepository, HashtagRepository>();
        services.AddScoped<IMentionRepository, MentionRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IModerationRepository, ModerationRepository>();
        services.AddScoped<IVisibilityRuleRepository, VisibilityRuleRepository>();
        services.AddScoped<IFeedSubscriptionRepository, FeedSubscriptionRepository>();
        services.AddScoped<IPollVoteRepository, PollVoteRepository>();

        // BG-05a. Community's own record of who exists, so a member can be a moderation subject
        // without this module reaching into Identity. See CommunityMember.
        services.AddScoped<ICommunityMemberDirectory, CommunityMemberDirectory>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddScoped<ContentReferenceResolver>();
        services.AddScoped<ViewerContextResolver>();
        services.AddScoped<CommunityAuthorNames>();
        services.AddScoped<VisibilityService>();
        services.AddScoped<ReactionCatalogLoader>();
        services.AddScoped<ReactionService>();
        services.AddScoped<CommentService>();
        services.AddScoped<BookmarkService>();
        services.AddScoped<ModerationService>();
        services.AddScoped<PostComposer>();
        services.AddScoped<FeedComposer>();
        services.AddScoped<MediaAttachmentClaimer>();
        services.AddScoped<CommunityIntegrationEventRelay>();
        services.AddScoped<AlertMapAnnouncer>();

        // Scoped rather than singleton: the catalogue is a per-request cache over a table, and a
        // singleton would go stale the moment a deployment retuned a reaction.
        services.AddScoped<ReactionCatalog>();
    }

    /// <summary>
    /// Registers every contributed extension, from every loaded platform assembly.
    /// <para>
    /// Scanning rather than listing is what makes "a new feed is a new class" literally true. A
    /// module ships an assembly and its feeds, rankings, reactions, resolvers and evaluators
    /// appear; this file does not change.
    /// </para>
    /// <para>
    /// <b>Everything here is scoped</b>, including the registries. Nearly every seam will hold a
    /// repository — a feed provider certainly does — and a singleton registry over scoped
    /// implementations is the captive dependency the host's provider validation refuses to start
    /// on. B4 recorded that exact failure with its country metadata catalogue, and paying one
    /// dictionary construction per request is a small price for not repeating it.
    /// </para>
    /// </summary>
    private static void RegisterSeams(IServiceCollection services)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        RegisterAll<IFeedProvider>(services, assemblies);
        RegisterAll<IRankingStrategy>(services, assemblies);
        RegisterAll<ICommunityContentSource>(services, assemblies);
        RegisterAll<IVisibilityEvaluator>(services, assemblies);
        RegisterAll<IMentionResolver>(services, assemblies);
        RegisterAll<IHashtagProcessor>(services, assemblies);
        RegisterAll<IContentReviewProvider>(services, assemblies);
        RegisterAll<IModerationPolicyProvider>(services, assemblies);
        RegisterAll<IContentEnricher>(services, assemblies);
        RegisterAll<IReactionCatalogContributor>(services, assemblies);

        services.AddScoped<FeedProviderRegistry>();
        services.AddScoped<RankingStrategyRegistry>();
        services.AddScoped<CommunityContentSourceRegistry>();
        services.AddScoped<VisibilityEvaluatorRegistry>();
        services.AddScoped<MentionExtractor>();
        services.AddScoped<HashtagExtractor>();
        services.AddScoped<ModerationPipeline>();
        services.AddScoped<ContentEnricherPipeline>();
    }

    /// <summary>
    /// The consumer contracts other modules bind to.
    /// <para>
    /// This is the surface the brief promises Guides, Community Updates and Community Life:
    /// comments, reactions, bookmarks, reports, visibility and social counts, available
    /// in-process to any module that registers a content source. Nothing else of Community is
    /// public to them.
    /// </para>
    /// </summary>
    private static void RegisterConsumerContracts(IServiceCollection services)
    {
        services.AddScoped<ICommunitySocialService, SocialSummaryService>();
        services.AddScoped<ICommunityCommentService>(
            provider => provider.GetRequiredService<CommentService>());
        services.AddScoped<ICommunityReactionService>(
            provider => provider.GetRequiredService<ReactionService>());
        services.AddScoped<ICommunityBookmarkService>(
            provider => provider.GetRequiredService<BookmarkService>());
        services.AddScoped<ICommunityModerationService>(
            provider => provider.GetRequiredService<ModerationService>());
        services.AddScoped<ICommunityVisibilityService>(
            provider => provider.GetRequiredService<VisibilityService>());
        services.AddScoped<ICommunitySearchProjectionSource, SearchProjectionService>();

        // Corroborated safety reports per place, for Geographic Intelligence's safety counts.
        services.AddScoped<ICommunitySafetyReports, CommunitySafetyReports>();
    }

    /// <summary>
    /// Hosted services the module registers itself, because it cannot edit <c>Program.cs</c>.
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddReferenceDataSeeder<ReactionCatalogSeeder>();
        services.AddHostedService<CommunitySweeper>();
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
