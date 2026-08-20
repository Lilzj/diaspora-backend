using Diaspora.Persistence;
using Diaspora.Persistence.Modules;
using Diaspora.Platform.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Diaspora.ArchitectureTests;

/// <summary>
/// The whole platform's EF model, built once, from every module's contributor.
/// <para>
/// <b>Discovered rather than listed.</b> The first model-reading test in this suite hard-coded
/// seven contributors, and two verticals later it was still reading seven — so Marketplace and
/// Housing were exempt from a platform rule by omission, silently, and nothing failed. Using the
/// same scanner the host uses means a rule written here applies to the module that lands next
/// without anybody remembering to add it.
/// </para>
/// <para>
/// A SQLite provider is used because building a model needs one, and no connection is ever opened.
/// The rules asserted against this model — owned-collection keys, concurrency tokens, backing
/// fields — are modelling decisions and identical on every provider.
/// </para>
/// </summary>
internal static class PlatformModel
{
    private static readonly Lazy<IModel> Model = new(Build);

    public static IModel Value => Model.Value;

    /// <summary>Every module's database contributor, found the way the host finds them.</summary>
    public static IReadOnlyList<IDatabaseContributor> Contributors { get; } =
        ModuleAssemblyScanner.CreateAll<IDatabaseContributor>(
            ModuleAssemblyScanner.FindPlatformAssemblies());

    private static IModel Build()
    {
        var options = new DbContextOptionsBuilder<DiasporaDbContext>()
            .UseSqlite("Data Source=:memory:")
            // A distinct model cache key, because EF caches one model per DbContext CLR type and
            // this suite builds the same context with two different contributor sets. Without it
            // whichever test ran first would decide what the other one was looking at.
            .ReplaceService<IModelCacheKeyFactory, PlatformModelCacheKeyFactory>()
            .Options;

        using var context = new DiasporaDbContext(
            options,
            currentUser: null,
            clock: null,
            eventDispatcher: null,
            Contributors);

        return context.Model;
    }

    private sealed class PlatformModelCacheKeyFactory : IModelCacheKeyFactory
    {
        public object Create(DbContext context, bool designTime) =>
            (typeof(PlatformModel), designTime);
    }
}
