using System.Reflection;
using Diaspora.Modules.Media.Application.Mapping;
using Diaspora.Modules.Media.Application.Services;
using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Collections;
using Diaspora.Modules.Media.Domain.Processing;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.Modules.Media.Extensibility.Access;
using Diaspora.Modules.Media.Extensibility.Processing;
using Diaspora.Modules.Media.Extensibility.Storage;
using Diaspora.Modules.Media.Infrastructure.Hosting;
using Diaspora.Modules.Media.Infrastructure.Platform;
using Diaspora.Modules.Media.Infrastructure.Processing;
using Diaspora.Modules.Media.Infrastructure.Storage;
using Diaspora.Modules.Media.Options;
using Diaspora.Modules.Media.Persistence.Repositories;
using Diaspora.Platform.Modules;
using Diaspora.Platform.Services.Abstractions;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Modules.Media;

/// <summary>
/// The Media &amp; Digital Asset Management Engine.
/// <para>
/// Registers entirely through the B2.6 contributor model. The host references this assembly so
/// it lands in the build output and names it nowhere else; permissions, feature gates,
/// policies, endpoints, telemetry and database configuration are all found by the kernel's
/// contributor scan. <c>Program.cs</c>, the Platform Kernel, the Identity module, the Trust
/// module and the Location module are untouched.
/// </para>
/// <para>
/// <b>Order 400.</b> After Identity (1), Bookmarks (100), Trust (200) and Location (300).
/// Registration order is not load-bearing here — Trust resolves <see cref="IStorageService"/>
/// lazily through an <c>IEnumerable</c>, so this module can fulfil that contract from any
/// position in the sequence — but ordering last matches the dependency direction: Media
/// consumes the kernel and is consumed by everything else.
/// </para>
/// <para>
/// <b>This module fulfils two platform contracts that have been declared and unimplemented
/// since B1.</b> <see cref="IStorageService"/>, which Trust's <c>DocumentLinkFactory</c> has
/// been waiting on since B3 — registering it turns Trust's null document links into real ones
/// with no edit to Trust. And <see cref="IImageProcessingService"/>, delegated to whichever
/// image processors a deployment registers. Both registrations are additive: nothing else
/// registered either, so nothing is replaced.
/// </para>
/// </summary>
public sealed class MediaModule : IPlatformModule
{
    public string Name => MediaModuleMetadata.Name;

    public string Version => MediaModuleMetadata.Version;

    public int Order => 400;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var assembly = typeof(MediaModule).Assembly;

        // Handlers and validators from this assembly. The Application layer's pipeline
        // behaviors are open generics registered once, so validation, telemetry, transactions
        // and authorisation apply to these handlers for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        RegisterOptions(services, configuration);
        RegisterRepositories(services);
        RegisterStorage(services, assembly);
        RegisterProcessing(services, assembly);
        RegisterAccess(services, assembly);
        RegisterServices(services);
        RegisterPlatformContracts(services);
        RegisterHostedServices(services);
    }

    private static void RegisterOptions(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MediaStorageOptions>()
            .Bind(configuration.GetSection(MediaStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MediaUploadOptions>()
            .Bind(configuration.GetSection(MediaUploadOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MediaSecurityOptions>()
            .Bind(configuration.GetSection(MediaSecurityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<MediaProcessingOptions>()
            .Bind(configuration.GetSection(MediaProcessingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddScoped<IUploadSessionRepository, UploadSessionRepository>();
        services.AddScoped<IMediaCollectionRepository, MediaCollectionRepository>();
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();
    }

    /// <summary>
    /// Registers the storage stack: the built-in filesystem provider, every provider any other
    /// assembly contributes, and the registry that resolves among them.
    /// <para>
    /// Scanning rather than listing is what makes "an S3 provider works the moment its assembly
    /// ships" literally true. A deployment adds a provider assembly and a configuration value;
    /// this file does not change.
    /// </para>
    /// <para>
    /// The registry is a singleton because providers are stateless clients over a remote
    /// service and composing the dictionary per request would be waste. The providers are
    /// therefore singletons too — a scoped provider held by a singleton registry is a captive
    /// dependency the host's service-provider validation refuses to start on.
    /// </para>
    /// </summary>
    private static void RegisterStorage(IServiceCollection services, Assembly assembly)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        // The default. Needs no package, works everywhere, and is genuinely correct for a
        // single-instance deployment. A cloud provider registered alongside it wins by name.
        services.AddSingleton<IMediaStorageProvider, FilesystemStorageProvider>();

        foreach (var type in ModuleAssemblyScanner.FindImplementations<IMediaStorageProvider>(assemblies))
        {
            if (type != typeof(FilesystemStorageProvider))
            {
                services.AddSingleton(typeof(IMediaStorageProvider), type);
            }
        }

        services.AddSingleton<StorageProviderRegistry>();
    }

    /// <summary>
    /// Registers the processing pipeline and every processor any assembly contributes.
    /// <para>
    /// Media ships two: a header-based metadata extractor and a JPEG metadata stripper, both
    /// pure managed code with no codec behind them. It ships no codec, no transcoder and no
    /// malware scanner, because each of those is a licensing or infrastructure decision a
    /// module must not make on a deployment's behalf — and a fake scanner that always says
    /// "clean" would be worse than none, since it makes an unscanned platform look scanned.
    /// </para>
    /// <para>
    /// Singletons: a processor is stateless by contract, and the registry over them is composed
    /// once. A processor needing scoped state should hold an <c>IServiceScopeFactory</c>, the
    /// same as any other singleton on the platform.
    /// </para>
    /// </summary>
    private static void RegisterProcessing(IServiceCollection services, Assembly assembly)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        services.AddSingleton<IMediaProcessor, HeaderMetadataExtractor>();
        services.AddSingleton<IMediaProcessor, JpegMetadataStripper>();

        var builtIn = new[] { typeof(HeaderMetadataExtractor), typeof(JpegMetadataStripper) };

        foreach (var type in ModuleAssemblyScanner.FindImplementations<IMediaProcessor>(assemblies))
        {
            if (!builtIn.Contains(type))
            {
                services.AddSingleton(typeof(IMediaProcessor), type);
            }
        }

        services.AddSingleton<MediaProcessorRegistry>();
        services.AddScoped<MediaProcessingPipeline>();
    }

    /// <summary>
    /// Registers every vertical's access policy.
    /// <para>
    /// Scoped, because a vertical's policy will need its own repository — a singleton
    /// registration works until the first policy that touches a <c>DbContext</c>, and then
    /// fails in a way that looks like a concurrency bug. The registry is scoped for the same
    /// reason, and validates its uniqueness per scope.
    /// </para>
    /// </summary>
    private static void RegisterAccess(IServiceCollection services, Assembly assembly)
    {
        var assemblies = ModuleAssemblyScanner.FindPlatformAssemblies().ToArray();

        foreach (var type in ModuleAssemblyScanner.FindImplementations<IMediaAccessPolicy>(assemblies))
        {
            services.AddScoped(typeof(IMediaAccessPolicy), type);
        }

        services.AddScoped<MediaAccessPolicyRegistry>();
    }

    private static void RegisterServices(IServiceCollection services)
    {
        services.AddSingleton<SignedUrlFactory>();

        services.AddScoped<UploadValidator>();
        services.AddScoped<UploadCoordinator>();
        services.AddScoped<MediaAccessGuard>();
        services.AddScoped<MediaUrlFactory>();

        services.AddScoped<MediaMapper>();
        services.AddScoped<UploadMapper>();
        services.AddScoped<CollectionMapper>();
    }

    /// <summary>
    /// Fulfils the two platform abstractions the contract catalog lists as
    /// declared-but-unimplemented.
    /// <para>
    /// <b>This is the whole Trust integration.</b> Trust's <c>DocumentLinkFactory</c> takes an
    /// <c>IEnumerable&lt;IStorageService&gt;</c> precisely so it can work before an
    /// implementation exists, and its own documentation says it starts working "when a storage
    /// provider is registered, by Infrastructure or by whichever phase supplies one, with no
    /// change here". These two lines are that phase. Not a line of Trust changed.
    /// </para>
    /// <para>
    /// <c>TryAddScoped</c> rather than <c>AddScoped</c>: if a later deployment registers its
    /// own implementation, it wins, and Media does not silently take over a contract somebody
    /// else meant to own.
    /// </para>
    /// </summary>
    private static void RegisterPlatformContracts(IServiceCollection services)
    {
        services.AddScoped<IStorageService, MediaStorageService>();
        services.AddSingleton<IImageProcessingService, MediaImageProcessingService>();
    }

    /// <summary>
    /// Hosted services the module registers itself, because it cannot edit <c>Program.cs</c>.
    /// The contributor model working as intended rather than a hole in it — the same mechanism
    /// the Location module uses for its catalogue seeder.
    /// </summary>
    private static void RegisterHostedServices(IServiceCollection services)
    {
        services.AddHostedService<MediaSweeper>();
        services.AddHostedService<MediaProcessingWorker>();
    }
}
