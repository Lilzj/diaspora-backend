using Diaspora.Contracts.Bookmarks.V1.Savers;
using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.Modules.Bookmarks.Extensibility;
using Diaspora.Modules.Bookmarks.Persistence;
using Diaspora.Platform.Modules;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Diaspora.Modules.Bookmarks;

/// <summary>
/// The Bookmarks module.
/// <para>
/// Discovered by the kernel's assembly scan — the host references this assembly so it lands
/// in the build output, and names it nowhere else. <c>ConfigureServices</c> registers only
/// the module's own repository; its handlers and validators are found by the Application
/// layer's assembly scan, and its permissions, gates, policies, telemetry, endpoints and
/// database configuration are found by the kernel's contributor scan.
/// </para>
/// <para>
/// That is the acceptance test for B2.6 made concrete: this file, and this module, exist
/// without a single edit to the Platform Kernel, the Identity module, or <c>Program.cs</c>.
/// Every other planned module plugs in exactly this way.
/// </para>
/// </summary>
public sealed class BookmarksModule : IPlatformModule
{
    public string Name => BookmarksModuleMetadata.Name;

    public string Version => BookmarksModuleMetadata.Version;

    // After Identity (Order 1): the module's endpoints authorise against tiers and
    // permissions Identity's subject accessor supplies, so identity must register first.
    public int Order => 100;

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = typeof(BookmarksModule).Assembly;

        // The module registers its own handlers and validators from its own assembly. The
        // Application layer's scan cannot see them — it runs before this module exists to
        // the container — but the pipeline behaviors it registered are open generics and
        // apply to these handlers all the same, so validation, telemetry, transactions and
        // authorisation come for free.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<IBookmarkRepository, BookmarkRepository>();

        // Who saved a thing, for a module composed later that must not reference this one —
        // Automation's `guide.savedChanged` (21 Oct 2026).
        services.AddScoped<ISaverDirectory, BookmarkSaverDirectory>();

        // The set of things that may be bookmarked. Singleton because it is composed once
        // from the baseline plus whatever modules contribute, and never changes after start.
        services.AddSingleton<BookmarkTargetCatalog>();
    }
}
